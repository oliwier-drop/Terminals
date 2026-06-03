using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using VtNetCore.VirtualTerminal;
using VtNetCore.VirtualTerminal.Layout;

namespace Terminals.Plugins.SshNet
{
    /// <summary>WinForms terminal surface backed by VtNetCore and GDI+ cell rendering.</summary>
    internal class SshTerminalControl : UserControl
    {
        private const int RenderIntervalMs = 50;
        private const int ResizeDebounceMs = 200;
        private const int MaxColumns = 260;
        private const int MaxRows = 100;

        private readonly Action<string> sendInput;
        private readonly SshVtSession session = new SshVtSession();
        private readonly StringBuilder pendingOutput = new StringBuilder();
        private readonly object pendingLock = new object();
        private readonly Timer renderTimer;
        private readonly Timer resizeDebounceTimer;
        private readonly VScrollBar scrollBar;
        private readonly Font terminalFont;
        private int cellWidth;
        private int cellHeight;

        private int viewTopRow;
        private int lastSessionColumns = -1;
        private int lastSessionRows = -1;
        private bool followTail = true;
        private bool pendingBeforeHandle;
        private bool caretVisible = true;
        private readonly Timer caretTimer;

        internal int Columns
        {
            get
            {
                this.RefreshCellMetrics();
                if (this.cellWidth <= 0)
                    return 80;
                int paintableWidth = this.GetPaintableWidth();
                return Math.Min(MaxColumns, Math.Max(20, paintableWidth / this.cellWidth));
            }
        }

        internal int Rows
        {
            get
            {
                this.RefreshCellMetrics();
                if (this.cellHeight <= 0)
                    return 24;
                return Math.Min(MaxRows, Math.Max(8, this.DisplayRectangle.Height / this.cellHeight));
            }
        }

        internal event EventHandler TerminalResized;

        internal SshTerminalControl(Action<string> sendInput)
        {
            this.sendInput = sendInput;
            this.terminalFont = CreateTerminalFont();
            this.RefreshCellMetrics();

            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Black;
            this.ForeColor = Color.Gainsboro;
            this.Font = this.terminalFont;
            this.TabStop = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.EnableDoubleBuffering();

            this.scrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Visible = false
            };
            this.scrollBar.Scroll += this.OnScrollBarScroll;
            this.Controls.Add(this.scrollBar);

            this.renderTimer = new Timer { Interval = RenderIntervalMs };
            this.renderTimer.Tick += this.OnRenderTimerTick;

            this.resizeDebounceTimer = new Timer { Interval = ResizeDebounceMs };
            this.resizeDebounceTimer.Tick += this.OnResizeDebounceTick;

            this.caretTimer = new Timer { Interval = 500 };
            this.caretTimer.Tick += this.OnCaretTimerTick;
            this.caretTimer.Start();

            this.Resize += this.OnTerminalResize;
            this.Layout += this.OnTerminalLayout;
            this.HandleCreated += this.OnHandleCreated;
            this.GotFocus += this.OnGotFocus;
            this.Click += this.OnClick;
            this.MouseWheel += this.OnMouseWheel;
        }

        internal void GetTerminalDimensions(out int columns, out int rows)
        {
            columns = this.Columns;
            rows = this.Rows;
        }

        internal void GetCellPixelSize(out int width, out int height)
        {
            this.RefreshCellMetrics();
            width = this.cellWidth;
            height = this.cellHeight;
        }

        internal void AppendAnsi(string text)
        {
            if (string.IsNullOrEmpty(text) || this.IsDisposed)
                return;

            lock (this.pendingLock)
            {
                this.pendingOutput.Append(text);
            }

            this.ScheduleRender();
        }

        internal void AppendAnsiAndFlush(string text)
        {
            if (string.IsNullOrEmpty(text) || this.IsDisposed)
                return;

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(this.AppendAnsiAndFlush), text);
                return;
            }

            lock (this.pendingLock)
            {
                this.pendingOutput.Append(text);
            }

            this.renderTimer.Stop();
            this.DrainAndRender();
        }

        internal void FlushPendingOutput()
        {
            if (this.IsDisposed)
                return;

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(this.FlushPendingOutput));
                return;
            }

            this.DrainAndRender();
        }

        internal void FocusTerminal()
        {
            if (this.IsDisposed)
                return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(this.FocusTerminal));
                return;
            }

            this.Focus();
        }

        internal void InvalidateRenderCache()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.renderTimer.Stop();
                this.renderTimer.Dispose();
                this.resizeDebounceTimer.Stop();
                this.resizeDebounceTimer.Dispose();
                this.caretTimer.Stop();
                this.caretTimer.Dispose();
                this.terminalFont.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Tab:
                case Keys.Back:
                case Keys.Return:
                case Keys.Escape:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Insert:
                case Keys.Delete:
                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            this.PaintTerminal(e.Graphics);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar))
                return;

            byte[] sequence;
            if (SshTerminalKeyInput.TryGetLetterSequence(
                this.session.Controller,
                e.KeyChar,
                (Control.ModifierKeys & Keys.Control) != 0,
                char.IsUpper(e.KeyChar) || (Control.ModifierKeys & Keys.Shift) != 0,
                out sequence))
            {
                string toSend = SshTerminalKeyInput.BytesToSendString(sequence);
                if (!string.IsNullOrEmpty(toSend))
                {
                    this.sendInput(toSend);
                    e.Handled = true;
                    return;
                }
            }

            this.sendInput(e.KeyChar.ToString());
            e.Handled = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V) || keyData == (Keys.Shift | Keys.Insert))
            {
                if (this.TryPasteFromClipboard())
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                if (this.TryPasteFromClipboard())
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    return;
                }
            }

            if (e.Shift && e.KeyCode == Keys.Insert)
            {
                if (this.TryPasteFromClipboard())
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    return;
                }
            }

            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
            {
                this.sendInput("\r");
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            byte[] sequence;
            if (SshTerminalKeyInput.TryGetSequence(
                this.session.Controller,
                e.KeyCode,
                e.Control,
                e.Shift,
                out sequence))
            {
                string toSend = SshTerminalKeyInput.BytesToSendString(sequence);
                if (!string.IsNullOrEmpty(toSend))
                {
                    this.sendInput(toSend);
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        private void OnHandleCreated(object sender, EventArgs e)
        {
            if (this.pendingBeforeHandle)
            {
                this.pendingBeforeHandle = false;
                this.ScheduleRender();
            }

            this.SyncSessionGeometry(force: true);
        }

        private void OnGotFocus(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        private void OnClick(object sender, EventArgs e)
        {
            this.Focus();
        }

        private void OnCaretTimerTick(object sender, EventArgs e)
        {
            if (!this.Focused)
                return;

            this.caretVisible = !this.caretVisible;
            this.InvalidateCaretRegion();
        }

        private void OnScrollBarScroll(object sender, ScrollEventArgs e)
        {
            this.followTail = false;
            this.viewTopRow = e.NewValue;
            this.Invalidate();
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (this.scrollBar.Maximum <= 0)
                return;

            int delta = e.Delta > 0 ? -3 : 3;
            this.followTail = false;
            this.viewTopRow = Math.Max(0, Math.Min(this.scrollBar.Maximum, this.viewTopRow + delta));
            this.scrollBar.Value = Math.Min(this.scrollBar.Maximum, this.viewTopRow);
            this.Invalidate();
        }

        private void ScheduleRender()
        {
            if (!this.IsHandleCreated)
            {
                this.pendingBeforeHandle = true;
                return;
            }

            if (!this.renderTimer.Enabled)
            {
                if (this.InvokeRequired)
                    this.BeginInvoke(new Action(this.StartRenderTimer));
                else
                    this.StartRenderTimer();
            }
        }

        private void StartRenderTimer()
        {
            if (!this.IsDisposed)
                this.renderTimer.Start();
        }

        private void OnRenderTimerTick(object sender, EventArgs e)
        {
            this.DrainAndRender();
        }

        private void DrainAndRender()
        {
            string chunk;
            lock (this.pendingLock)
            {
                if (this.pendingOutput.Length == 0)
                {
                    this.renderTimer.Stop();
                    return;
                }

                chunk = this.pendingOutput.ToString();
                this.pendingOutput.Length = 0;
            }

            this.session.Push(chunk);

            if (this.session.ConsumeChangedFlag() || chunk.Length > 0)
            {
                this.UpdateScrollRange();
                if (this.followTail)
                    this.viewTopRow = this.scrollBar.Maximum;

                this.Invalidate();
            }
        }

        private void OnTerminalResize(object sender, EventArgs e)
        {
            this.ScheduleTerminalResize();
        }

        private void OnTerminalLayout(object sender, LayoutEventArgs e)
        {
            this.ScheduleTerminalResize();
        }

        private void ScheduleTerminalResize()
        {
            this.resizeDebounceTimer.Stop();
            this.resizeDebounceTimer.Start();
        }

        private void RefreshCellMetrics()
        {
            this.cellWidth = MeasureMonospaceCellWidth(this.terminalFont);
            this.cellHeight = Math.Max(1, this.terminalFont.Height);
        }

        private static int MeasureMonospaceCellWidth(Font font)
        {
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            int twoChars = TextRenderer.MeasureText("00", font, Size.Empty, flags).Width;
            int oneChar = TextRenderer.MeasureText("0", font, Size.Empty, flags).Width;
            int delta = twoChars - oneChar;
            if (delta > 0)
                return delta;

            int tenChars = TextRenderer.MeasureText("MMMMMMMMMM", font, Size.Empty, flags).Width;
            return Math.Max(1, tenChars / 10);
        }

        private int GetPaintableWidth()
        {
            int width = this.DisplayRectangle.Width;
            if (this.scrollBar.Visible)
                width -= this.scrollBar.Width;
            return Math.Max(0, width);
        }

        private static int MeasureTextWidth(string text, Font font)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return TextRenderer.MeasureText(
                text,
                font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        }

        private static Font CreateSpanFont(Font baseFont, LayoutSpan span)
        {
            FontStyle style = FontStyle.Regular;
            if (span.Bold)
                style |= FontStyle.Bold;
            if (span.Italic)
                style |= FontStyle.Italic;

            return style == FontStyle.Regular ? baseFont : new Font(baseFont, style);
        }

        private static int GetPixelXForColumn(LayoutRow layoutRow, int targetColumn, Font baseFont)
        {
            if (layoutRow == null || layoutRow.Spans == null)
                return targetColumn * MeasureMonospaceCellWidth(baseFont);

            int column = 0;
            int x = 0;
            foreach (LayoutSpan span in layoutRow.Spans)
            {
                string text = span.Text ?? string.Empty;
                if (text.Length == 0)
                    continue;

                if (span.Hidden)
                {
                    column += text.Length;
                    continue;
                }

                Font spanFont = null;
                try
                {
                    spanFont = CreateSpanFont(baseFont, span);
                    if (column + text.Length > targetColumn)
                    {
                        int offsetInSpan = Math.Max(0, targetColumn - column);
                        x += MeasureTextWidth(text.Substring(0, offsetInSpan), spanFont);
                        return x;
                    }

                    x += MeasureTextWidth(text, spanFont);
                    column += text.Length;
                }
                finally
                {
                    if (!ReferenceEquals(spanFont, baseFont))
                        spanFont.Dispose();
                }
            }

            return x;
        }

        private bool TryPasteFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                    return false;

                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text))
                    return false;

                text = text.Replace("\r\n", "\n").Replace('\r', '\n');
                this.sendInput(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnResizeDebounceTick(object sender, EventArgs e)
        {
            this.resizeDebounceTimer.Stop();
            this.SyncSessionGeometry();
            this.UpdateScrollRange();
            this.Invalidate();

            if (this.TerminalResized != null)
                this.TerminalResized(this, EventArgs.Empty);
        }

        internal void SyncSessionGeometry(bool force = false)
        {
            this.ApplySessionSize(this.Columns, this.Rows, force);
        }

        internal void ApplySessionSize(int columns, int rows, bool force = false)
        {
            if (!force && columns == this.lastSessionColumns && rows == this.lastSessionRows)
                return;

            this.lastSessionColumns = columns;
            this.lastSessionRows = rows;
            this.session.Resize(columns, rows);
        }

        private void UpdateScrollRange()
        {
            int maxTop = Math.Max(0, this.session.Controller.ViewPort.TopRow);
            this.scrollBar.Maximum = maxTop;
            this.scrollBar.LargeChange = Math.Max(1, this.Rows);
            this.scrollBar.Visible = maxTop > 0;

            if (this.followTail)
            {
                this.viewTopRow = maxTop;
                if (this.scrollBar.Maximum >= this.scrollBar.Minimum)
                    this.scrollBar.Value = Math.Min(this.scrollBar.Maximum, this.viewTopRow);
            }
        }

        private void PaintTerminal(Graphics graphics)
        {
            graphics.Clear(Color.Black);
            int paintWidth = this.GetPaintableWidth();
            if (paintWidth <= 0 || this.cellHeight <= 0)
                return;

            var controller = this.session.Controller;
            int paintColumns = Math.Max(1, Math.Max(controller.VisibleColumns, this.Columns));
            int paintRows = Math.Max(1, controller.VisibleRows);
            List<LayoutRow> rows = controller.GetPageSpans(
                this.viewTopRow,
                paintRows,
                paintColumns,
                null);

            if (rows == null)
                return;

            float y = 0;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                LayoutRow row = rows[rowIndex];
                if (row == null || row.Spans == null)
                {
                    y += this.cellHeight;
                    continue;
                }

                int x = 0;
                foreach (LayoutSpan span in row.Spans)
                {
                    if (span == null || span.Hidden)
                        continue;

                    Color backColor = VtNetColorHelper.ParseBackground(span.BackgroundColor);
                    Color foreColor = VtNetColorHelper.ParseForeground(span.ForgroundColor);
                    string text = span.Text ?? string.Empty;

                    if (text.Length == 0)
                        continue;

                    Font spanFont = null;
                    try
                    {
                        spanFont = CreateSpanFont(this.terminalFont, span);
                        int spanWidth = MeasureTextWidth(text, spanFont);
                        var cellRect = new Rectangle(x, (int)y, spanWidth, this.cellHeight);
                        TextRenderer.DrawText(
                            graphics,
                            text,
                            spanFont,
                            cellRect,
                            foreColor,
                            backColor,
                            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                        x += spanWidth;
                    }
                    finally
                    {
                        if (!ReferenceEquals(spanFont, this.terminalFont))
                            spanFont.Dispose();
                    }
                }

                y += this.cellHeight;
            }

            if (this.Focused && this.caretVisible)
            {
                this.PaintCaret(graphics, controller, rows, paintWidth);
            }
        }

        private void PaintCaret(
            Graphics graphics,
            VirtualTerminalController controller,
            List<LayoutRow> rows,
            int paintWidth)
        {
            if (!controller.CursorState.ShowCursor)
                return;

            TextPosition cursor = controller.ViewPort.CursorPosition;
            int row = cursor.Row + (controller.ViewPort.TopRow - this.viewTopRow);
            int visibleRows = Math.Max(1, controller.VisibleRows);
            if (row < 0 || row >= visibleRows)
                return;

            int column = Math.Max(0, Math.Min(cursor.Column, Math.Max(0, controller.VisibleColumns - 1)));
            LayoutRow layoutRow = row < rows.Count ? rows[row] : null;
            int x = GetPixelXForColumn(layoutRow, column, this.terminalFont);
            int y = row * this.cellHeight;
            if (x >= paintWidth)
                return;

            using (var brush = new SolidBrush(this.ForeColor))
            {
                graphics.FillRectangle(brush, x, y, Math.Max(2, this.cellWidth / 8), this.cellHeight);
            }
        }

        private void InvalidateCaretRegion()
        {
            if (!this.IsHandleCreated)
                return;

            var controller = this.session.Controller;
            TextPosition cursor = controller.ViewPort.CursorPosition;
            int row = cursor.Row + (controller.ViewPort.TopRow - this.viewTopRow);
            if (row < 0 || row >= this.Rows)
                return;

            int paintColumns = Math.Max(1, Math.Max(controller.VisibleColumns, this.Columns));
            int paintRows = Math.Max(1, controller.VisibleRows);
            List<LayoutRow> rows = controller.GetPageSpans(
                this.viewTopRow,
                paintRows,
                paintColumns,
                null);
            LayoutRow layoutRow = rows != null && row < rows.Count ? rows[row] : null;
            int x = GetPixelXForColumn(layoutRow, cursor.Column, this.terminalFont);

            var rect = new Rectangle(
                x,
                row * this.cellHeight,
                Math.Max(this.cellWidth, 2),
                this.cellHeight);
            this.Invalidate(rect);
        }

        private static Font CreateTerminalFont()
        {
            try
            {
                return new Font("Consolas", 10f, FontStyle.Regular);
            }
            catch
            {
                return new Font(FontFamily.GenericMonospace, 10f, FontStyle.Regular);
            }
        }

        private void EnableDoubleBuffering()
        {
            typeof(Control).InvokeMember(
                "DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                this,
                new object[] { true });
        }
    }
}
