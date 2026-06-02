using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// Minimal VT100/xterm terminal buffer for interactive SSH output.
    /// </summary>
    internal sealed class AnsiTerminalScreen
    {
        private const int MaxLines = 5000;

        private readonly List<TerminalLine> lines = new List<TerminalLine>();
        private AnsiStyle currentStyle = AnsiStyle.Default();
        private int cursorRow;
        private int cursorCol;
        private int savedRow;
        private int savedCol;

        private enum ParseState
        {
            Normal,
            Escape,
            Csi
        }

        private ParseState parseState = ParseState.Normal;
        private readonly StringBuilder csiBuffer = new StringBuilder();

        internal AnsiTerminalScreen()
        {
            this.lines.Add(new TerminalLine());
        }

        internal void Feed(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (char character in text)
                this.ProcessCharacter(character);
        }

        internal void RenderTo(RichTextBox target, Font font)
        {
            target.Clear();
            foreach (TerminalLine line in this.lines)
                line.AppendTo(target, font);

            target.SelectionStart = target.TextLength;
            target.ScrollToCaret();
        }

        internal string RenderPlainTextForTest()
        {
            var builder = new StringBuilder();
            foreach (TerminalLine line in this.lines)
            {
                builder.Append(line.ToPlainText());
                builder.Append('\n');
            }

            return builder.ToString();
        }

        private void ProcessCharacter(char character)
        {
            switch (this.parseState)
            {
                case ParseState.Normal:
                    if (character == '\x1B')
                        this.parseState = ParseState.Escape;
                    else
                        this.WriteCharacter(character);
                    break;

                case ParseState.Escape:
                    if (character == '[')
                    {
                        this.csiBuffer.Length = 0;
                        this.parseState = ParseState.Csi;
                    }
                    else
                        this.parseState = ParseState.Normal;
                    break;

                case ParseState.Csi:
                    if (character >= 0x40 && character <= 0x7E)
                    {
                        this.ExecuteCsi(this.csiBuffer.ToString(), character);
                        this.parseState = ParseState.Normal;
                    }
                    else
                        this.csiBuffer.Append(character);
                    break;
            }
        }

        private void WriteCharacter(char character)
        {
            switch (character)
            {
                case '\r':
                    this.cursorCol = 0;
                    break;
                case '\n':
                    this.NewLine();
                    break;
                case '\b':
                    if (this.cursorCol > 0)
                        this.cursorCol--;
                    break;
                case '\t':
                    this.cursorCol = ((this.cursorCol / 8) + 1) * 8;
                    this.EnsureLineWidth();
                    break;
                case '\a':
                    break;
                default:
                    if (character < ' ')
                        break;
                    this.EnsureCursorLine();
                    this.lines[this.cursorRow].SetCell(this.cursorCol, character, this.currentStyle);
                    this.cursorCol++;
                    this.EnsureLineWidth();
                    break;
            }
        }

        private void NewLine()
        {
            this.cursorRow++;
            this.cursorCol = 0;
            while (this.lines.Count <= this.cursorRow)
                this.lines.Add(new TerminalLine());
            this.TrimExcessLines();
        }

        private void EnsureCursorLine()
        {
            while (this.lines.Count <= this.cursorRow)
                this.lines.Add(new TerminalLine());
        }

        private void EnsureLineWidth()
        {
            this.EnsureCursorLine();
            this.lines[this.cursorRow].EnsureWidth(this.cursorCol + 1);
        }

        private void TrimExcessLines()
        {
            if (this.lines.Count <= MaxLines)
                return;

            int remove = this.lines.Count - MaxLines;
            this.lines.RemoveRange(0, remove);
            this.cursorRow = Math.Max(0, this.cursorRow - remove);
            this.savedRow = Math.Max(0, this.savedRow - remove);
        }

        private void ExecuteCsi(string parameters, char command)
        {
            int[] args = ParseParameters(parameters);
            switch (command)
            {
                case 'm':
                    this.ApplySgr(args);
                    break;
                case 'H':
                case 'f':
                    this.cursorRow = Math.Max(0, GetArg(args, 0, 1) - 1);
                    this.cursorCol = Math.Max(0, GetArg(args, 1, 1) - 1);
                    this.EnsureCursorLine();
                    break;
                case 'A':
                    this.cursorRow = Math.Max(0, this.cursorRow - GetArg(args, 0, 1));
                    break;
                case 'B':
                    this.cursorRow += GetArg(args, 0, 1);
                    this.EnsureCursorLine();
                    break;
                case 'C':
                    this.cursorCol += GetArg(args, 0, 1);
                    this.EnsureLineWidth();
                    break;
                case 'D':
                    this.cursorCol = Math.Max(0, this.cursorCol - GetArg(args, 0, 1));
                    break;
                case 'G':
                    this.cursorCol = Math.Max(0, GetArg(args, 0, 1) - 1);
                    this.EnsureLineWidth();
                    break;
                case 'J':
                    this.EraseDisplay(GetArg(args, 0, 0));
                    break;
                case 'K':
                    this.EraseLine(GetArg(args, 0, 0));
                    break;
                case 's':
                    this.savedRow = this.cursorRow;
                    this.savedCol = this.cursorCol;
                    break;
                case 'u':
                    this.cursorRow = this.savedRow;
                    this.cursorCol = this.savedCol;
                    this.EnsureCursorLine();
                    break;
            }
        }

        private void EraseDisplay(int mode)
        {
            switch (mode)
            {
                case 0:
                    this.EraseFromCursorToEndOfScreen();
                    break;
                case 1:
                    this.EraseFromStartToCursor();
                    break;
                case 2:
                case 3:
                    this.lines.Clear();
                    this.lines.Add(new TerminalLine());
                    this.cursorRow = 0;
                    this.cursorCol = 0;
                    break;
            }
        }

        private void EraseLine(int mode)
        {
            this.EnsureCursorLine();
            TerminalLine line = this.lines[this.cursorRow];
            switch (mode)
            {
                case 0:
                    line.EraseFrom(this.cursorCol);
                    break;
                case 1:
                    line.EraseTo(this.cursorCol);
                    break;
                case 2:
                    line.Clear();
                    break;
            }
        }

        private void EraseFromCursorToEndOfScreen()
        {
            this.EnsureCursorLine();
            this.lines[this.cursorRow].EraseFrom(this.cursorCol);
            for (int i = this.lines.Count - 1; i > this.cursorRow; i--)
                this.lines.RemoveAt(i);
        }

        private void EraseFromStartToCursor()
        {
            this.EnsureCursorLine();
            this.lines[this.cursorRow].EraseTo(this.cursorCol);
            for (int i = 0; i < this.cursorRow; i++)
                this.lines[i].Clear();
        }

        private void ApplySgr(int[] codes)
        {
            if (codes.Length == 0)
            {
                this.currentStyle = AnsiStyle.Default();
                return;
            }

            for (int i = 0; i < codes.Length; i++)
            {
                int code = codes[i];
                switch (code)
                {
                    case 0:
                        this.currentStyle = AnsiStyle.Default();
                        break;
                    case 1:
                        this.currentStyle.Bold = true;
                        break;
                    case 22:
                        this.currentStyle.Bold = false;
                        break;
                    case 30: this.currentStyle.ForeColor = Color.Black; break;
                    case 31: this.currentStyle.ForeColor = Color.IndianRed; break;
                    case 32: this.currentStyle.ForeColor = Color.LightGreen; break;
                    case 33: this.currentStyle.ForeColor = Color.Khaki; break;
                    case 34: this.currentStyle.ForeColor = Color.LightSkyBlue; break;
                    case 35: this.currentStyle.ForeColor = Color.Plum; break;
                    case 36: this.currentStyle.ForeColor = Color.MediumTurquoise; break;
                    case 37: this.currentStyle.ForeColor = Color.Gainsboro; break;
                    case 39: this.currentStyle.ForeColor = Color.Gainsboro; break;
                    case 40: this.currentStyle.BackColor = Color.Black; break;
                    case 41: this.currentStyle.BackColor = Color.DarkRed; break;
                    case 42: this.currentStyle.BackColor = Color.DarkGreen; break;
                    case 43: this.currentStyle.BackColor = Color.Olive; break;
                    case 44: this.currentStyle.BackColor = Color.DarkBlue; break;
                    case 45: this.currentStyle.BackColor = Color.DarkMagenta; break;
                    case 46: this.currentStyle.BackColor = Color.DarkCyan; break;
                    case 47: this.currentStyle.BackColor = Color.LightGray; break;
                    case 49: this.currentStyle.BackColor = Color.Black; break;
                    case 90: this.currentStyle.ForeColor = Color.Gray; break;
                    case 91: this.currentStyle.ForeColor = Color.Salmon; break;
                    case 92: this.currentStyle.ForeColor = Color.PaleGreen; break;
                    case 93: this.currentStyle.ForeColor = Color.Wheat; break;
                    case 94: this.currentStyle.ForeColor = Color.LightBlue; break;
                    case 95: this.currentStyle.ForeColor = Color.Violet; break;
                    case 96: this.currentStyle.ForeColor = Color.Aquamarine; break;
                    case 97: this.currentStyle.ForeColor = Color.White; break;
                }
            }
        }

        private static int[] ParseParameters(string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
                return new int[0];

            string[] parts = parameters.Split(';');
            var values = new List<int>();
            foreach (string part in parts)
            {
                int value;
                if (int.TryParse(part, out value))
                    values.Add(value);
                else if (part.Length == 0)
                    values.Add(0);
            }

            return values.ToArray();
        }

        private static int GetArg(int[] args, int index, int defaultValue)
        {
            if (args.Length <= index)
                return defaultValue;
            return args[index] == 0 ? defaultValue : args[index];
        }

        private sealed class TerminalLine
        {
            private readonly List<TerminalCell> cells = new List<TerminalCell>();

            internal void EnsureWidth(int width)
            {
                while (this.cells.Count < width)
                    this.cells.Add(TerminalCell.Empty);
            }

            internal void SetCell(int column, char character, AnsiStyle style)
            {
                this.EnsureWidth(column + 1);
                this.cells[column] = new TerminalCell(character, style.Clone());
            }

            internal void EraseFrom(int column)
            {
                if (column < this.cells.Count)
                    this.cells.RemoveRange(column, this.cells.Count - column);
            }

            internal void EraseTo(int column)
            {
                int count = Math.Min(column + 1, this.cells.Count);
                if (count > 0)
                    this.cells.RemoveRange(0, count);
            }

            internal void Clear()
            {
                this.cells.Clear();
            }

            internal string ToPlainText()
            {
                if (this.cells.Count == 0)
                    return string.Empty;

                int end = this.cells.Count - 1;
                while (end >= 0 && this.cells[end].IsEmpty)
                    end--;

                var builder = new StringBuilder();
                for (int i = 0; i <= end; i++)
                {
                    if (!this.cells[i].IsEmpty)
                        builder.Append(this.cells[i].Character);
                }

                return builder.ToString();
            }

            internal void AppendTo(RichTextBox target, Font font)
            {
                if (this.cells.Count == 0)
                {
                    target.AppendText(Environment.NewLine);
                    return;
                }

                int end = this.cells.Count - 1;
                while (end >= 0 && this.cells[end].IsEmpty)
                    end--;

                for (int i = 0; i <= end; i++)
                {
                    TerminalCell cell = this.cells[i];
                    if (cell.IsEmpty)
                        continue;

                    target.SelectionStart = target.TextLength;
                    target.SelectionLength = 0;
                    target.SelectionColor = cell.Style.ForeColor;
                    target.SelectionBackColor = cell.Style.BackColor;
                    target.SelectionFont = new Font(font, cell.Style.Bold ? FontStyle.Bold : FontStyle.Regular);
                    target.AppendText(cell.Character.ToString());
                }

                target.AppendText(Environment.NewLine);
            }
        }

        private struct TerminalCell
        {
            internal static readonly TerminalCell Empty = new TerminalCell('\0', AnsiStyle.Default());

            internal readonly char Character;
            internal readonly AnsiStyle Style;

            internal TerminalCell(char character, AnsiStyle style)
            {
                this.Character = character;
                this.Style = style;
            }

            internal bool IsEmpty
            {
                get { return this.Character == '\0'; }
            }
        }
    }
}
