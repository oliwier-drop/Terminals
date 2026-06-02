using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// xterm-style terminal buffer (fixed rows/cols + scrollback).
    /// Control sequences: https://invisible-island.net/xterm/ctlseqs/ctlseqs.html
    /// </summary>
    internal sealed class AnsiTerminalScreen
    {
        private const int MaxScrollbackLines = 5000;
        private const int DefaultWidth = 80;
        private const int DefaultHeight = 24;

        private readonly List<string> scrollback = new List<string>();
        private TerminalRow[] primary;
        private TerminalRow[] alternate;
        private bool alternateActive;

        private int width = DefaultWidth;
        private int height = DefaultHeight;
        private int scrollTop;
        private int scrollBottom;
        private int cursorRow;
        private int cursorCol;
        private AnsiStyle currentStyle = AnsiStyle.Default();

        private int savedRow;
        private int savedCol;
        private int decSavedRow;
        private int decSavedCol;
        private int altSavedCursorRow;
        private int altSavedCursorCol;

        private string lastRenderedPlain;

        private enum ParseState
        {
            Normal,
            Escape,
            Csi,
            Osc
        }

        private ParseState parseState = ParseState.Normal;
        private readonly StringBuilder csiBuffer = new StringBuilder();

        internal int TerminalWidth
        {
            get { return this.width; }
            set { this.Resize(value, this.height); }
        }

        internal int TerminalHeight
        {
            get { return this.height; }
            set { this.Resize(this.width, Math.Max(2, value)); }
        }

        internal int CursorRow
        {
            get { return this.scrollback.Count + this.cursorRow; }
        }

        internal int CursorColumn
        {
            get { return this.cursorCol; }
        }

        internal AnsiTerminalScreen()
        {
            this.primary = this.CreateRows(DefaultHeight);
            this.alternate = this.CreateRows(DefaultHeight);
            this.ResetScrollRegion();
        }

        internal void Feed(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (char character in text)
                this.ProcessCharacter(character);
        }

        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        internal void RenderPlainTo(RichTextBox target, int maxLines, out int caretIndex)
        {
            if (maxLines <= 0)
                maxLines = this.scrollback.Count + this.height;

            var builder = new StringBuilder();
            caretIndex = 0;

            int scrollBudget = Math.Max(0, maxLines - this.height);
            int scrollStart = Math.Max(0, this.scrollback.Count - scrollBudget);
            int cursorGlobalRow = this.scrollback.Count + this.cursorRow;

            for (int i = scrollStart; i < this.scrollback.Count; i++)
            {
                if (builder.Length > 0)
                    builder.Append('\n');

                if (i == cursorGlobalRow)
                    caretIndex = builder.Length + Math.Min(this.cursorCol, this.scrollback[i].Length);

                builder.Append(this.scrollback[i]);
            }

            for (int row = 0; row < this.height; row++)
            {
                int globalRow = this.scrollback.Count + row;
                string lineText = this.ActiveScreen[row].ToPlainText(this.width);

                if (builder.Length > 0)
                    builder.Append('\n');

                if (globalRow == cursorGlobalRow)
                    caretIndex = builder.Length + Math.Min(this.cursorCol, lineText.Length);

                builder.Append(lineText);
            }

            if (caretIndex == 0 && builder.Length > 0 && cursorGlobalRow >= scrollStart + (this.scrollback.Count - scrollStart) + this.height)
                caretIndex = builder.Length;

            string text = TrimTrailingEmptyLines(builder.ToString());

            if (caretIndex > text.Length)
                caretIndex = text.Length;

            bool textChanged = !string.Equals(text, this.lastRenderedPlain, StringComparison.Ordinal);

            if (target.IsHandleCreated)
                SendMessage(target.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

            bool wasReadOnly = target.ReadOnly;
            target.ReadOnly = false;
            try
            {
                if (textChanged)
                {
                    target.Text = text;
                    this.lastRenderedPlain = text;
                }

                caretIndex = Math.Max(0, Math.Min(caretIndex, target.TextLength));
                target.SelectionStart = caretIndex;
                target.SelectionLength = 0;
                target.ScrollToCaret();
            }
            finally
            {
                target.ReadOnly = wasReadOnly;
            }

            if (target.IsHandleCreated)
                SendMessage(target.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
        }

        internal string RenderPlainTextForTest()
        {
            var builder = new StringBuilder();
            foreach (string line in this.scrollback)
            {
                builder.Append(line);
                builder.Append('\n');
            }

            for (int row = 0; row < this.height; row++)
            {
                builder.Append(this.ActiveScreen[row].ToPlainText(this.width));
                builder.Append('\n');
            }

            return TrimTrailingEmptyLines(builder.ToString()) + "\n";
        }

        internal void ResetRenderCache()
        {
            this.lastRenderedPlain = null;
        }

        private TerminalRow[] ActiveScreen
        {
            get { return this.alternateActive ? this.alternate : this.primary; }
        }

        private void Resize(int newWidth, int newHeight)
        {
            newWidth = Math.Max(1, newWidth);
            newHeight = Math.Max(2, newHeight);

            if (newWidth == this.width && newHeight == this.height)
                return;

            this.primary = this.ResizeRows(this.primary, newWidth, newHeight);
            this.alternate = this.ResizeRows(this.alternate, newWidth, newHeight);
            this.width = newWidth;
            this.height = newHeight;
            this.ClampCursor();
            this.ResetScrollRegion();
            this.lastRenderedPlain = null;
        }

        private TerminalRow[] ResizeRows(TerminalRow[] source, int newWidth, int newHeight)
        {
            var rows = this.CreateRows(newHeight);
            int copy = Math.Min(source.Length, newHeight);
            for (int i = 0; i < copy; i++)
                rows[i] = source[i].Resize(newWidth);
            return rows;
        }

        private TerminalRow[] CreateRows(int rowCount)
        {
            var rows = new TerminalRow[rowCount];
            for (int i = 0; i < rowCount; i++)
                rows[i] = new TerminalRow(this.width);
            return rows;
        }

        private void ResetScrollRegion()
        {
            this.scrollTop = 0;
            this.scrollBottom = this.height - 1;
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
                    else if (character == ']')
                        this.parseState = ParseState.Osc;
                    else if (character == '7')
                    {
                        this.decSavedRow = this.cursorRow;
                        this.decSavedCol = this.cursorCol;
                        this.parseState = ParseState.Normal;
                    }
                    else if (character == '8')
                    {
                        this.cursorRow = this.decSavedRow;
                        this.cursorCol = this.decSavedCol;
                        this.ClampCursor();
                        this.parseState = ParseState.Normal;
                    }
                    else
                        this.parseState = ParseState.Normal;
                    break;

                case ParseState.Osc:
                    if (character == '\a' || character == '\x1B')
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
                    this.LineFeed();
                    break;
                case '\b':
                    if (this.cursorCol > 0)
                    {
                        this.cursorCol--;
                        this.ActiveScreen[this.cursorRow].SetCell(this.cursorCol, ' ', this.currentStyle, this.width);
                    }
                    break;
                case '\t':
                    this.cursorCol = Math.Min(this.width - 1, ((this.cursorCol / 8) + 1) * 8);
                    break;
                case '\a':
                    break;
                default:
                    if (character < ' ')
                        break;

                    this.ActiveScreen[this.cursorRow].SetCell(this.cursorCol, character, this.currentStyle, this.width);
                    this.cursorCol++;
                    if (this.cursorCol >= this.width)
                        this.LineFeed();
                    break;
            }
        }

        private void LineFeed()
        {
            if (this.cursorRow >= this.scrollBottom)
                this.ScrollUp(1);
            else
            {
                this.cursorRow++;
                this.ClampCursor();
            }

            this.cursorCol = 0;
        }

        private void ScrollUp(int count)
        {
            for (int n = 0; n < count; n++)
            {
                if (this.scrollTop == 0 && !this.alternateActive)
                    this.PushScrollbackLine(this.ActiveScreen[this.scrollTop].ToPlainText(this.width));

                for (int row = this.scrollTop; row < this.scrollBottom; row++)
                    this.ActiveScreen[row] = this.ActiveScreen[row + 1].Clone();

                this.ActiveScreen[this.scrollBottom] = new TerminalRow(this.width);
            }
        }

        private void ScrollDown(int count)
        {
            for (int n = 0; n < count; n++)
            {
                for (int row = this.scrollBottom; row > this.scrollTop; row--)
                    this.ActiveScreen[row] = this.ActiveScreen[row - 1].Clone();

                this.ActiveScreen[this.scrollTop] = new TerminalRow(this.width);
            }
        }

        private void PushScrollbackLine(string line)
        {
            this.scrollback.Add(line);
            if (this.scrollback.Count > MaxScrollbackLines)
                this.scrollback.RemoveAt(0);
        }

        private void ClampCursor()
        {
            this.cursorRow = Math.Max(this.scrollTop, Math.Min(this.scrollBottom, this.cursorRow));
            this.cursorCol = Math.Max(0, Math.Min(this.width - 1, this.cursorCol));
        }

        private void ExecuteCsi(string parameters, char command)
        {
            if (parameters.Length > 0 && parameters[0] == '?')
            {
                this.ExecuteDecPrivateMode(parameters, command);
                return;
            }

            int[] args = ParseParameters(parameters);
            switch (command)
            {
                case 'm':
                    this.ApplySgr(args);
                    break;
                case 'H':
                case 'f':
                    this.cursorRow = Math.Max(0, Math.Min(this.height - 1, GetArg(args, 0, 1) - 1));
                    this.cursorCol = Math.Max(0, Math.Min(this.width - 1, GetArg(args, 1, 1) - 1));
                    this.ClampCursor();
                    break;
                case 'A':
                    this.cursorRow = Math.Max(this.scrollTop, this.cursorRow - GetArg(args, 0, 1));
                    break;
                case 'B':
                case 'e':
                    this.cursorRow = Math.Min(this.scrollBottom, this.cursorRow + GetArg(args, 0, 1));
                    break;
                case 'C':
                    this.cursorCol = Math.Min(this.width - 1, this.cursorCol + GetArg(args, 0, 1));
                    break;
                case 'D':
                    this.cursorCol = Math.Max(0, this.cursorCol - GetArg(args, 0, 1));
                    break;
                case 'G':
                case '`':
                    this.cursorCol = Math.Max(0, Math.Min(this.width - 1, GetArg(args, 0, 1) - 1));
                    break;
                case 'd':
                    this.cursorRow = Math.Max(0, Math.Min(this.height - 1, GetArg(args, 0, 1) - 1));
                    this.ClampCursor();
                    break;
                case 'J':
                    this.EraseDisplay(GetArg(args, 0, 0));
                    break;
                case 'K':
                    this.EraseLine(GetArg(args, 0, 0));
                    break;
                case 'r':
                    if (args.Length >= 2)
                    {
                        this.scrollTop = Math.Max(0, Math.Min(this.height - 1, GetArg(args, 0, 1) - 1));
                        this.scrollBottom = Math.Max(this.scrollTop, Math.Min(this.height - 1, GetArg(args, 1, this.height) - 1));
                    }
                    else
                        this.ResetScrollRegion();

                    this.ClampCursor();
                    break;
                case 'S':
                    this.ScrollUp(GetArg(args, 0, 1));
                    break;
                case 'T':
                    this.ScrollDown(GetArg(args, 0, 1));
                    break;
                case 'L':
                    this.InsertLines(GetArg(args, 0, 1));
                    break;
                case 'M':
                    this.DeleteLines(GetArg(args, 0, 1));
                    break;
                case 'P':
                    this.DeleteCharacters(GetArg(args, 0, 1));
                    break;
                case '@':
                    this.InsertCharacters(GetArg(args, 0, 1));
                    break;
                case 's':
                    this.savedRow = this.cursorRow;
                    this.savedCol = this.cursorCol;
                    break;
                case 'u':
                    this.cursorRow = this.savedRow;
                    this.cursorCol = this.savedCol;
                    this.ClampCursor();
                    break;
                case 'X':
                    this.ActiveScreen[this.cursorRow].EraseFrom(this.cursorCol, GetArg(args, 0, 0), this.width);
                    break;
            }
        }

        private void InsertLines(int count)
        {
            count = Math.Max(1, count);
            int bottom = Math.Min(this.scrollBottom, this.cursorRow + count - 1);
            for (int row = this.scrollBottom; row > bottom; row--)
                this.ActiveScreen[row] = this.ActiveScreen[row - count].Clone();

            for (int row = this.cursorRow; row <= bottom; row++)
                this.ActiveScreen[row] = new TerminalRow(this.width);
        }

        private void DeleteLines(int count)
        {
            count = Math.Max(1, count);
            for (int row = this.cursorRow; row <= this.scrollBottom - count; row++)
                this.ActiveScreen[row] = this.ActiveScreen[row + count].Clone();

            for (int row = this.scrollBottom - count + 1; row <= this.scrollBottom; row++)
                this.ActiveScreen[row] = new TerminalRow(this.width);
        }

        private void InsertCharacters(int count)
        {
            this.ActiveScreen[this.cursorRow].InsertBlanks(this.cursorCol, count, this.width, this.currentStyle);
        }

        private void DeleteCharacters(int count)
        {
            this.ActiveScreen[this.cursorRow].DeleteCharacters(this.cursorCol, count, this.width);
        }

        private void ExecuteDecPrivateMode(string parameters, char command)
        {
            int[] args = ParseParameters(parameters.StartsWith("?")
                ? parameters.Substring(1)
                : parameters);

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != 1049)
                    continue;

                if (command == 'h')
                    this.EnterAlternateScreen();
                else if (command == 'l')
                    this.LeaveAlternateScreen();
            }
        }

        private void EnterAlternateScreen()
        {
            this.altSavedCursorRow = this.cursorRow;
            this.altSavedCursorCol = this.cursorCol;

            for (int row = 0; row < this.height; row++)
                this.alternate[row] = new TerminalRow(this.width);

            this.cursorRow = 0;
            this.cursorCol = 0;
            this.alternateActive = true;
            this.ResetScrollRegion();
        }

        private void LeaveAlternateScreen()
        {
            this.alternateActive = false;
            this.cursorRow = this.altSavedCursorRow;
            this.cursorCol = this.altSavedCursorCol;
            this.ClampCursor();
            this.ResetScrollRegion();
        }

        private void EraseDisplay(int mode)
        {
            switch (mode)
            {
                case 0:
                    this.ActiveScreen[this.cursorRow].EraseFrom(this.cursorCol, 0, this.width);
                    for (int row = this.cursorRow + 1; row < this.height; row++)
                        this.ActiveScreen[row] = new TerminalRow(this.width);
                    break;
                case 1:
                    for (int row = 0; row < this.cursorRow; row++)
                        this.ActiveScreen[row] = new TerminalRow(this.width);
                    this.ActiveScreen[this.cursorRow].EraseTo(this.cursorCol, this.width);
                    break;
                case 2:
                case 3:
                    for (int row = 0; row < this.height; row++)
                        this.ActiveScreen[row] = new TerminalRow(this.width);
                    this.cursorRow = 0;
                    this.cursorCol = 0;
                    if (mode == 3 && !this.alternateActive)
                        this.scrollback.Clear();
                    break;
            }
        }

        private void EraseLine(int mode)
        {
            TerminalRow line = this.ActiveScreen[this.cursorRow];
            switch (mode)
            {
                case 0:
                    line.EraseFrom(this.cursorCol, 0, this.width);
                    break;
                case 1:
                    line.EraseTo(this.cursorCol, this.width);
                    break;
                case 2:
                    this.ActiveScreen[this.cursorRow] = new TerminalRow(this.width);
                    break;
            }
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

        internal bool TryGetCellStyleForTest(int row, int column, out AnsiStyle style)
        {
            style = AnsiStyle.Default();
            if (column < 0 || column >= this.width)
                return false;

            if (row < this.scrollback.Count)
            {
                if (column >= this.scrollback[row].Length)
                    return false;
                return true;
            }

            int viewportRow = row - this.scrollback.Count;
            if (viewportRow < 0 || viewportRow >= this.height)
                return false;

            style = this.ActiveScreen[viewportRow].GetStyle(column);
            return true;
        }

        private static string TrimTrailingEmptyLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            int end = text.Length;
            while (end > 0)
            {
                int lineStart = text.LastIndexOf('\n', end - 1);
                int segmentStart = lineStart < 0 ? 0 : lineStart + 1;
                string segment = text.Substring(segmentStart, end - segmentStart);
                if (segment.Length > 0)
                    break;
                end = segmentStart > 0 ? segmentStart - 1 : 0;
            }

            return end <= 0 ? string.Empty : text.Substring(0, end);
        }

        private sealed class TerminalRow
        {
            private readonly TerminalCell[] cells;

            internal TerminalRow(int width)
            {
                this.cells = new TerminalCell[Math.Max(1, width)];
                for (int i = 0; i < this.cells.Length; i++)
                    this.cells[i] = TerminalCell.Empty;
            }

            internal TerminalRow Resize(int newWidth)
            {
                var row = new TerminalRow(newWidth);
                int copy = Math.Min(this.cells.Length, newWidth);
                for (int i = 0; i < copy; i++)
                    row.cells[i] = this.cells[i];
                return row;
            }

            internal TerminalRow Clone()
            {
                var row = new TerminalRow(this.cells.Length);
                for (int i = 0; i < this.cells.Length; i++)
                    row.cells[i] = this.cells[i];
                return row;
            }

            internal AnsiStyle GetStyle(int column)
            {
                if (column < 0 || column >= this.cells.Length)
                    return AnsiStyle.Default();

                return this.cells[column].IsEmpty ? AnsiStyle.Default() : this.cells[column].Style;
            }

            internal void SetCell(int column, char character, AnsiStyle style, int width)
            {
                if (column < 0 || column >= width)
                    return;

                this.EnsureWidth(width);
                this.cells[column] = new TerminalCell(character, style);
            }

            internal void InsertBlanks(int column, int count, int width, AnsiStyle style)
            {
                this.EnsureWidth(width);
                count = Math.Max(1, count);
                for (int i = width - 1; i >= column + count; i--)
                    this.cells[i] = this.cells[i - count];

                for (int i = column; i < column + count && i < width; i++)
                    this.cells[i] = new TerminalCell(' ', style);
            }

            internal void DeleteCharacters(int column, int count, int width)
            {
                this.EnsureWidth(width);
                count = Math.Max(1, count);
                for (int i = column; i < width - count; i++)
                    this.cells[i] = this.cells[i + count];

                for (int i = width - count; i < width; i++)
                    this.cells[i] = TerminalCell.Empty;
            }

            internal void EraseFrom(int column, int eraseCount, int width)
            {
                this.EnsureWidth(width);
                int end = eraseCount > 0 ? Math.Min(width, column + eraseCount) : width;
                for (int i = column; i < end; i++)
                    this.cells[i] = TerminalCell.Empty;
            }

            internal void EraseTo(int column, int width)
            {
                this.EnsureWidth(width);
                for (int i = 0; i <= column && i < width; i++)
                    this.cells[i] = TerminalCell.Empty;
            }

            internal string ToPlainText(int width)
            {
                this.EnsureWidth(width);
                int end = width - 1;
                while (end >= 0 && this.cells[end].IsEmpty)
                    end--;

                if (end < 0)
                    return string.Empty;

                var builder = new StringBuilder(end + 1);
                for (int i = 0; i <= end; i++)
                {
                    if (!this.cells[i].IsEmpty)
                        builder.Append(this.cells[i].Character);
                }

                return builder.ToString();
            }

            private void EnsureWidth(int width)
            {
                if (this.cells.Length == width)
                    return;

                throw new InvalidOperationException("Terminal row width mismatch.");
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
