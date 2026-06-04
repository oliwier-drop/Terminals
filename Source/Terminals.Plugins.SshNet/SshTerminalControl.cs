// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors ? fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Terminals.Plugins.SshNet.Rendering;
using VtNetCore.VirtualTerminal;

namespace Terminals.Plugins.SshNet
{
    /// <summary>WinForms terminal surface backed by VtNetCore and glyph-atlas rendering.</summary>
    internal class SshTerminalControl : UserControl
    {
        private const int RenderIntervalMs = 16;
        private const int ResizeDebounceMs = 200;
        private const int CoalescedRenderThresholdChars = 8192;
        private const int MaxColumns = 260;
        private const int MaxRows = 100;

        private readonly Action<string> sendInput;
        private readonly SshVtSession session = new SshVtSession();
        private readonly TerminalRenderPipeline renderPipeline = new TerminalRenderPipeline();
        private readonly StringBuilder pendingOutput = new StringBuilder();
        private readonly object pendingLock = new object();
        private readonly Timer renderTimer;
        private readonly Timer resizeDebounceTimer;
        private readonly VScrollBar scrollBar;
        private int cellWidth;
        private int cellHeight;
        private int cachedMetricsWidth = -1;
        private int cachedMetricsHeight = -1;
        private Bitmap frameCache;
        private int frameCacheWidth;
        private int frameCacheHeight;

        private int viewTopRow;
        private int lastSessionColumns = -1;
        private int lastSessionRows = -1;
        private bool followTail = true;
        private bool pendingBeforeHandle;
        private bool caretVisible = true;
        private readonly Timer caretTimer;
        private bool isSelecting;
        private bool hasSelection;
        private TerminalCellPoint selectionAnchor;
        private TerminalCellPoint selectionEnd;
        private Rectangle lastSelectionInvalidateRect = Rectangle.Empty;

        internal int Columns
        {
            get
            {
                this.EnsureCellMetrics();
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
                this.EnsureCellMetrics();
                if (this.cellHeight <= 0)
                    return 24;
                return Math.Min(MaxRows, Math.Max(8, this.DisplayRectangle.Height / this.cellHeight));
            }
        }

        internal event EventHandler TerminalResized;

        internal SshTerminalControl(Action<string> sendInput)
        {
            this.sendInput = sendInput;
            this.renderPipeline.UpdateDpiScale(this.GetDpiScale());
            this.RefreshCellMetrics();

            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Black;
            this.ForeColor = Color.Gainsboro;
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
            this.MouseDown += this.OnMouseDown;
            this.MouseMove += this.OnMouseMove;
            this.MouseUp += this.OnMouseUp;
            this.MouseWheel += this.OnMouseWheel;
        }

        internal void GetTerminalDimensions(out int columns, out int rows)
        {
            columns = this.Columns;
            rows = this.Rows;
        }

        internal void GetCellPixelSize(out int width, out int height)
        {
            this.EnsureCellMetrics();
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
                this.renderPipeline.Dispose();
                if (this.frameCache != null)
                {
                    this.frameCache.Dispose();
                    this.frameCache = null;
                }
            }

            base.Dispose(disposing);
        }

        protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
        {
            if (e.Control || e.Alt)
            {
                Keys key = e.KeyCode;
                if ((key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9))
                    e.IsInputKey = true;
            }

            base.OnPreviewKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if ((keyData & Keys.Control) != 0 || (keyData & Keys.Alt) != 0)
            {
                if ((key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9))
                    return true;
            }

            switch (key)
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
            if (this.frameCache != null)
                e.Graphics.DrawImageUnscaled(this.frameCache, 0, 0);
            else
                this.RebuildFrameCache();

            this.PaintSelectionOverlay(e.Graphics);

            if (this.Focused && this.caretVisible)
                this.PaintCaretOverlay(e.Graphics);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar))
            {
                if ((Control.ModifierKeys & Keys.Control) != 0
                    && e.KeyChar > 0
                    && e.KeyChar < 32)
                {
                    char letter = (char)('A' + e.KeyChar - 1);
                    byte[] ctrlSequence;
                    if (SshTerminalKeyInput.TryGetLetterSequence(
                        this.session.Controller,
                        letter,
                        true,
                        (Control.ModifierKeys & Keys.Shift) != 0,
                        out ctrlSequence))
                    {
                        string toSend = SshTerminalKeyInput.BytesToSendString(ctrlSequence);
                        if (!string.IsNullOrEmpty(toSend))
                        {
                            this.sendInput(toSend);
                            e.Handled = true;
                            return;
                        }
                    }
                }

                return;
            }

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

            bool control = (keyData & Keys.Control) != 0;
            bool alt = (keyData & Keys.Alt) != 0;
            if (control || alt)
            {
                if (this.TrySendKeySequence(keyData, out string _))
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

            if (e.KeyCode == Keys.Back)
            {
                if (this.TrySendKeySequence(
                    e.KeyCode | (e.Shift ? Keys.Shift : Keys.None),
                    out string backspace))
                {
                    this.sendInput(backspace);
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    return;
                }
            }

            if (this.TrySendKeySequence(
                (e.KeyCode | (e.Control ? Keys.Control : Keys.None) | (e.Shift ? Keys.Shift : Keys.None) | (e.Alt ? Keys.Alt : Keys.None)),
                out string toSend))
            {
                this.sendInput(toSend);
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private bool TrySendKeySequence(Keys keyData, out string toSend)
        {
            toSend = null;
            Keys key = keyData & Keys.KeyCode;
            bool control = (keyData & Keys.Control) != 0;
            bool shift = (keyData & Keys.Shift) != 0;
            bool alt = (keyData & Keys.Alt) != 0;

            return SshTerminalKeyInput.TrySendFromKeyEvent(
                this.session.Controller,
                key,
                control,
                shift,
                alt,
                out toSend);
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

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            this.Focus();
            if (!this.TryHitTestCell(e.X, e.Y, out int row, out int col))
                return;

            if (this.hasSelection)
                this.InvalidateSelectionRegion();

            this.isSelecting = true;
            this.hasSelection = true;
            this.selectionAnchor = new TerminalCellPoint(row, col);
            this.selectionEnd = new TerminalCellPoint(row, col);
            this.Capture = true;
            this.lastSelectionInvalidateRect = Rectangle.Empty;
            this.InvalidateSelectionRegion();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!this.isSelecting || (e.Button & MouseButtons.Left) == 0)
                return;

            if (!this.TryHitTestCellForSelection(e.X, e.Y, out int row, out int col))
                return;

            if (row == this.selectionEnd.Row && col == this.selectionEnd.Column)
                return;

            this.selectionEnd = new TerminalCellPoint(row, col);
            this.InvalidateSelectionRegion();
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (this.isSelecting)
                {
                    this.isSelecting = false;
                    this.Capture = false;
                    if (this.TryHitTestCellForSelection(e.X, e.Y, out int row, out int col))
                        this.selectionEnd = new TerminalCellPoint(row, col);

                    this.CopySelectionToClipboard();
                    this.InvalidateSelectionRegion();
                }

                return;
            }

            if (e.Button != MouseButtons.Right)
                return;

            this.Focus();
            this.TryPasteFromClipboard();
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
            int rowDelta = e.NewValue - this.viewTopRow;
            this.viewTopRow = e.NewValue;
            this.ApplyViewportScroll(rowDelta);
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (this.scrollBar.Maximum <= 0)
                return;

            int lineDelta = e.Delta > 0 ? -3 : 3;
            this.followTail = false;
            int newTop = Math.Max(0, Math.Min(this.scrollBar.Maximum, this.viewTopRow + lineDelta));
            int rowDelta = newTop - this.viewTopRow;
            this.viewTopRow = newTop;
            this.scrollBar.Value = Math.Min(this.scrollBar.Maximum, this.viewTopRow);
            this.ApplyViewportScroll(rowDelta);
        }

        private void ScheduleRender()
        {
            if (!this.IsHandleCreated)
            {
                this.pendingBeforeHandle = true;
                return;
            }

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(this.ScheduleRender));
                return;
            }

            int pendingChars;
            lock (this.pendingLock)
                pendingChars = this.pendingOutput.Length;

            if (pendingChars >= CoalescedRenderThresholdChars)
            {
                if (!this.renderTimer.Enabled && !this.IsDisposed)
                    this.renderTimer.Start();
                return;
            }

            this.DrainAndRender();
            if (!this.renderTimer.Enabled && !this.IsDisposed)
                this.renderTimer.Start();
        }

        private void OnRenderTimerTick(object sender, EventArgs e)
        {
            this.DrainAndRender();
            lock (this.pendingLock)
            {
                if (this.pendingOutput.Length == 0)
                    this.renderTimer.Stop();
            }
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

            if (!this.session.ConsumeChangedFlag())
                return;

            int viewTopBefore = this.viewTopRow;
            this.UpdateScrollRange();
            if (this.followTail)
                this.viewTopRow = this.scrollBar.Maximum;

            int scrollDeltaRows = this.viewTopRow - viewTopBefore;
            this.EnsureFrameCacheBitmap();
            if (this.frameCache == null)
                return;

            bool forceFullRepaint = TerminalRenderPipeline.ChunkRequiresFullRepaint(chunk);

            using (Graphics graphics = Graphics.FromImage(this.frameCache))
            {
                if (scrollDeltaRows != 0
                    && !forceFullRepaint
                    && !this.renderPipeline.TryScrollFrame(
                        graphics,
                        this.frameCache,
                        this.session.Controller,
                        this.viewTopRow,
                        scrollDeltaRows,
                        this.frameCacheWidth))
                {
                    forceFullRepaint = true;
                }

                var diffOptions = new TerminalRowDiffOptions { ForceFullRepaint = forceFullRepaint };
                var dirtyRows = this.renderPipeline.UpdateFrame(
                    graphics,
                    this.session.Controller,
                    this.viewTopRow,
                    this.frameCacheWidth,
                    this.frameCacheHeight,
                    diffOptions);
                this.InvalidateDirtyRows(dirtyRows, forceFullRepaint);
                if (this.hasSelection)
                    this.InvalidateSelectionRegion();
            }
        }

        private void OnTerminalResize(object sender, EventArgs e)
        {
            this.InvalidateCellMetrics();
            this.ScheduleTerminalResize();
        }

        private void OnTerminalLayout(object sender, LayoutEventArgs e)
        {
            this.InvalidateCellMetrics();
            this.ScheduleTerminalResize();
        }

        private void InvalidateCellMetrics()
        {
            this.cachedMetricsWidth = -1;
            this.cachedMetricsHeight = -1;
        }

        private void ScheduleTerminalResize()
        {
            this.resizeDebounceTimer.Stop();
            this.resizeDebounceTimer.Start();
        }

        private void EnsureCellMetrics()
        {
            int width = this.GetPaintableWidth();
            int height = this.DisplayRectangle.Height;
            if (width == this.cachedMetricsWidth && height == this.cachedMetricsHeight && this.cellWidth > 0)
                return;

            this.RefreshCellMetrics();
            this.cachedMetricsWidth = width;
            this.cachedMetricsHeight = height;
        }

        private void RefreshCellMetrics()
        {
            this.renderPipeline.UpdateDpiScale(this.GetDpiScale());
            this.cellWidth = this.renderPipeline.CellWidth;
            this.cellHeight = this.renderPipeline.CellHeight;
        }

        private float GetDpiScale()
        {
            if (!this.IsHandleCreated)
                return 1f;

            using (Graphics graphics = this.CreateGraphics())
                return graphics.DpiX / 96f;
        }

        private int GetPaintableWidth()
        {
            int width = this.DisplayRectangle.Width;
            if (this.scrollBar.Visible)
                width -= this.scrollBar.Width;
            return Math.Max(0, width);
        }

        private void InvalidateDirtyRows(System.Collections.Generic.IList<int> dirtyRows, bool fullRepaint)
        {
            if (fullRepaint || dirtyRows == null || dirtyRows.Count == 0)
            {
                this.Invalidate();
                return;
            }

            int minRow = dirtyRows[0];
            int maxRow = dirtyRows[0];
            for (int i = 1; i < dirtyRows.Count; i++)
            {
                if (dirtyRows[i] < minRow)
                    minRow = dirtyRows[i];
                if (dirtyRows[i] > maxRow)
                    maxRow = dirtyRows[i];
            }

            int top = minRow * this.cellHeight;
            int height = (maxRow - minRow + 1) * this.cellHeight;
            this.Invalidate(new Rectangle(0, top, this.frameCacheWidth, height));
        }

        private void EnsureFrameCacheBitmap()
        {
            int width = this.GetPaintableWidth();
            int height = Math.Max(1, this.DisplayRectangle.Height);
            if (width <= 0 || this.cellHeight <= 0)
                return;

            if (this.frameCache != null
                && this.frameCacheWidth == width
                && this.frameCacheHeight == height)
            {
                return;
            }

            if (this.frameCache != null)
                this.frameCache.Dispose();

            this.frameCache = new Bitmap(width, height);
            this.frameCacheWidth = width;
            this.frameCacheHeight = height;
            this.renderPipeline.InvalidatePreviousGrid();
        }

        private void RebuildFrameCache()
        {
            this.EnsureFrameCacheBitmap();
            if (this.frameCache == null)
                return;

            using (Graphics graphics = Graphics.FromImage(this.frameCache))
            {
                this.renderPipeline.RebuildFullFrame(
                    graphics,
                    this.session.Controller,
                    this.viewTopRow,
                    this.frameCacheWidth);
            }
        }

        private void ApplyViewportScroll(int scrollDeltaRows)
        {
            if (scrollDeltaRows == 0)
                return;

            this.EnsureFrameCacheBitmap();
            if (this.frameCache == null)
                return;

            using (Graphics graphics = Graphics.FromImage(this.frameCache))
            {
                if (!this.renderPipeline.TryScrollFrame(
                    graphics,
                    this.frameCache,
                    this.session.Controller,
                    this.viewTopRow,
                    scrollDeltaRows,
                    this.frameCacheWidth))
                {
                    this.renderPipeline.RebuildFullFrame(
                        graphics,
                        this.session.Controller,
                        this.viewTopRow,
                        this.frameCacheWidth);
                }
            }

            this.Invalidate();
        }

        private bool TryHitTestCell(int x, int y, out int row, out int column)
        {
            row = 0;
            column = 0;
            this.EnsureCellMetrics();
            if (this.cellWidth <= 0 || this.cellHeight <= 0)
                return false;

            int paintableWidth = this.GetPaintableWidth();
            if (x < 0 || x >= paintableWidth || y < 0)
                return false;

            row = y / this.cellHeight;
            column = x / this.cellWidth;
            int maxRow = Math.Max(1, this.Rows) - 1;
            int maxCol = Math.Max(1, this.Columns) - 1;
            if (row > maxRow || column > maxCol)
                return false;

            return true;
        }

        private void CopySelectionToClipboard()
        {
            if (!this.hasSelection)
                return;

            if (this.selectionAnchor.Row == this.selectionEnd.Row
                && this.selectionAnchor.Column == this.selectionEnd.Column)
            {
                return;
            }

            try
            {
                var grid = TerminalCellGridBuilder.Build(
                    this.session.Controller,
                    this.viewTopRow,
                    Math.Max(1, this.Rows),
                    Math.Max(1, this.Columns));
                string text = TerminalTextSelection.ExtractTextFromGrid(
                    grid,
                    this.selectionAnchor,
                    this.selectionEnd);

                if (string.IsNullOrEmpty(text))
                    return;

                Clipboard.SetText(text);
            }
            catch
            {
            }
        }

        private void PaintSelectionOverlay(Graphics graphics)
        {
            if (!this.hasSelection || graphics == null)
                return;

            var grid = TerminalCellGridBuilder.Build(
                this.session.Controller,
                this.viewTopRow,
                Math.Max(1, this.Rows),
                Math.Max(1, this.Columns));
            this.renderPipeline.PaintSelection(
                graphics,
                grid,
                this.selectionAnchor,
                this.selectionEnd);
        }

        private void InvalidateSelectionRegion()
        {
            if (!this.IsHandleCreated)
                return;

            Rectangle newRect = Rectangle.Empty;
            if (this.hasSelection)
            {
                this.EnsureCellMetrics();
                newRect = TerminalTextSelection.GetSelectionPixelBounds(
                    this.selectionAnchor,
                    this.selectionEnd,
                    Math.Max(1, this.Columns),
                    this.cellWidth,
                    this.cellHeight);
            }

            if (newRect.IsEmpty && !this.hasSelection)
            {
                if (!this.lastSelectionInvalidateRect.IsEmpty)
                {
                    this.Invalidate(this.lastSelectionInvalidateRect);
                    this.lastSelectionInvalidateRect = Rectangle.Empty;
                }

                return;
            }

            if (newRect.IsEmpty)
                return;

            Rectangle invalidateRect = this.lastSelectionInvalidateRect.IsEmpty
                ? newRect
                : Rectangle.Union(this.lastSelectionInvalidateRect, newRect);
            this.lastSelectionInvalidateRect = newRect;
            this.Invalidate(invalidateRect);
        }

        private void ClearSelection()
        {
            if (!this.hasSelection)
                return;

            this.hasSelection = false;
            this.isSelecting = false;
            this.InvalidateSelectionRegion();
        }

        private bool TryHitTestCellForSelection(int x, int y, out int row, out int column)
        {
            if (this.TryHitTestCell(x, y, out row, out column))
                return true;

            this.EnsureCellMetrics();
            if (this.cellWidth <= 0 || this.cellHeight <= 0)
                return false;

            int maxRow = Math.Max(1, this.Rows) - 1;
            int maxCol = Math.Max(1, this.Columns) - 1;
            int paintableWidth = this.GetPaintableWidth();
            int clientHeight = this.DisplayRectangle.Height;

            if (y < 0)
                row = 0;
            else if (y >= clientHeight)
                row = maxRow;
            else
                row = y / this.cellHeight;

            if (x < 0)
                column = 0;
            else if (x >= paintableWidth)
                column = maxCol;
            else
                column = x / this.cellWidth;

            row = Math.Min(maxRow, Math.Max(0, row));
            column = Math.Min(maxCol, Math.Max(0, column));
            return true;
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
            this.InvalidateCellMetrics();
            this.SyncSessionGeometry();
            this.UpdateScrollRange();
            this.renderPipeline.InvalidatePreviousGrid();
            this.RebuildFrameCache();
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

        private void PaintCaretOverlay(Graphics graphics)
        {
            var controller = this.session.Controller;
            if (!controller.CursorState.ShowCursor)
                return;

            TextPosition cursor = controller.ViewPort.CursorPosition;
            int row = cursor.Row + (controller.ViewPort.TopRow - this.viewTopRow);
            int visibleRows = Math.Max(1, controller.VisibleRows);
            if (row < 0 || row >= visibleRows)
                return;

            int column = Math.Max(0, Math.Min(cursor.Column, Math.Max(0, controller.VisibleColumns - 1)));
            int x = column * this.cellWidth;
            int y = row * this.cellHeight;
            if (x >= this.GetPaintableWidth())
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

            int x = cursor.Column * this.cellWidth;
            var rect = new Rectangle(
                x,
                row * this.cellHeight,
                Math.Max(this.cellWidth, 2),
                this.cellHeight);
            this.Invalidate(rect);
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
