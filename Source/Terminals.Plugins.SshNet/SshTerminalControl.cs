// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using Terminals.Plugins.SshNet.Rendering;
using VtNetCore.VirtualTerminal;

namespace Terminals.Plugins.SshNet
{
    /// <summary>WinForms terminal surface backed by VtNetCore and SkiaSharp row rendering.</summary>
    internal class SshTerminalControl : UserControl
    {
        private const int RenderIntervalMs = 16;
        private const int LocalResizeCoalesceMs = 16;
        private const int PtyResizeDebounceMs = 50;
        private const int CoalescedRenderThresholdChars = 4096;
        private const int ImmediateRenderThresholdChars = 512;
        private const int CatchUpPendingChars = 4096;
        private const int AggressiveCatchUpPendingChars = 16384;
        private const int MaxCharsPerDrain = 65536;
        private const int CatchUpMaxCharsPerDrain = 262144;
        private const int CatchUpTimerPasses = 4;
        private const int RenderFrameBudgetRows = 32;
        private const int MaxColumns = 260;
        private const int MaxRows = 100;
        private const int SelectionAutoScrollIntervalMs = 50;
        private const int SelectionAutoScrollRows = 1;

        private readonly Action<string> sendInput;
        private readonly SshVtSession session = new SshVtSession();
        private readonly TerminalRenderPipeline renderPipeline = new TerminalRenderPipeline();
        private readonly SshLocalEchoController localEcho = new SshLocalEchoController();
        private readonly Queue<string> pendingChunks = new Queue<string>();
        private readonly object pendingLock = new object();
        private readonly List<int> deferredPaintRows = new List<int>();
        private float userZoomFactor = 1f;
        private float cachedDisplayPointSize = -1f;
        private float cachedDpiScale = -1f;
        private readonly Timer renderTimer;
        private readonly Timer localResizeCoalesceTimer;
        private readonly Timer ptyResizeDebounceTimer;
        private readonly VScrollBar scrollBar;
        private readonly Timer selectionAutoScrollTimer;
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
        private Point lastSelectionMouseLocation;
        private bool updatingScrollBar;
        private bool lastAlternateScreenActive;

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
            this.renderPipeline.UpdateDisplayScale(
                TerminalDisplayScale.ComputePointSize(1f, 800, 600, this.userZoomFactor));
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

            this.localResizeCoalesceTimer = new Timer { Interval = LocalResizeCoalesceMs };
            this.localResizeCoalesceTimer.Tick += this.OnLocalResizeCoalesceTick;

            this.ptyResizeDebounceTimer = new Timer { Interval = PtyResizeDebounceMs };
            this.ptyResizeDebounceTimer.Tick += this.OnPtyResizeDebounceTick;

            this.selectionAutoScrollTimer = new Timer { Interval = SelectionAutoScrollIntervalMs };
            this.selectionAutoScrollTimer.Tick += this.OnSelectionAutoScrollTimerTick;

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

        /// <summary>Sync VT buffer size with the control and return columns used for line wrapping.</summary>
        internal int PrepareOutputColumns()
        {
            this.SyncSessionGeometry(force: true);
            int columns = this.session.Columns;
            if (columns < 1)
                columns = this.Columns;
            return Math.Max(columns, 1);
        }

        internal void GetCellPixelSize(out int width, out int height)
        {
            this.EnsureCellMetrics();
            width = this.cellWidth;
            height = this.cellHeight;
        }

        internal bool IsAlternateScreenActive
        {
            get { return this.session.IsAlternateScreenActive; }
        }

        internal void AppendAnsi(string text)
        {
            if (string.IsNullOrEmpty(text) || this.IsDisposed)
                return;

            lock (this.pendingLock)
            {
                this.pendingChunks.Enqueue(text);
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
                this.pendingChunks.Enqueue(text);
            }

            this.renderTimer.Stop();
            this.DrainAndRender();
        }

        internal void AppendServerAnsi(string text)
        {
            if (string.IsNullOrEmpty(text) || this.IsDisposed)
                return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(this.AppendServerAnsi), text);
                return;
            }

            string filtered = this.localEcho.FilterServerOutput(text);
            if (!string.IsNullOrEmpty(filtered))
                this.AppendAnsi(filtered);
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
                this.localResizeCoalesceTimer.Stop();
                this.localResizeCoalesceTimer.Dispose();
                this.ptyResizeDebounceTimer.Stop();
                this.ptyResizeDebounceTimer.Dispose();
                this.selectionAutoScrollTimer.Stop();
                this.selectionAutoScrollTimer.Dispose();
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
            this.EnsureCellMetrics();

            int paintableWidth = this.GetPaintableWidth();
            int paintableHeight = Math.Max(1, this.DisplayRectangle.Height);
            bool sizeMismatch = this.frameCache == null
                || this.frameCache.Width != paintableWidth
                || this.frameCache.Height != paintableHeight;

            if (sizeMismatch && paintableWidth > 0 && this.cellHeight > 0)
                this.ScheduleLocalResizeRepaint();

            if (this.frameCache != null)
            {
                int drawWidth = Math.Min(this.frameCache.Width, paintableWidth);
                int drawHeight = Math.Min(this.frameCache.Height, paintableHeight);
                if (drawWidth > 0 && drawHeight > 0)
                {
                    if (!sizeMismatch)
                        e.Graphics.DrawImageUnscaled(this.frameCache, 0, 0);
                    else
                    {
                        e.Graphics.DrawImage(
                            this.frameCache,
                            new Rectangle(0, 0, drawWidth, drawHeight),
                            new Rectangle(0, 0, drawWidth, drawHeight),
                            GraphicsUnit.Pixel);
                    }
                }
            }

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
                    if (toSend == e.KeyChar.ToString())
                        this.SendPrintableInput(toSend, e.KeyChar);
                    else
                        this.sendInput(toSend);
                    e.Handled = true;
                    return;
                }
            }

            this.SendPrintableInput(e.KeyChar.ToString(), e.KeyChar);
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

            if (e.Control
                && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
            {
                this.ApplyZoomDelta(0.05f);
                e.Handled = true;
                return;
            }

            if (e.Control
                && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
            {
                this.ApplyZoomDelta(-0.05f);
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.D0)
            {
                if (Math.Abs(this.userZoomFactor - 1f) > 0.01f)
                {
                    this.userZoomFactor = 1f;
                    this.InvalidateCellMetrics();
                    this.ApplyImmediateLocalResizeRepaint();
                    this.SchedulePtyResizeNotification();
                }

                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
            {
                this.localEcho.NotifyUserInput("\r");
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
                    this.SendBackspaceInput(backspace);
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

        private void SendPrintableInput(string text, char keyChar)
        {
            this.sendInput(text);
            this.localEcho.NotifyUserInput(text);

            if (this.session.IsAlternateScreenActive)
                return;

            string screenText = this.session.GetScreenText();
            if (this.localEcho.IsPasswordEntryActive(screenText))
            {
                this.localEcho.RegisterPasswordKeySuppressor();
                return;
            }

            string localEchoText;
            if (this.localEcho.TryCreatePrintableEcho(
                keyChar,
                screenText,
                this.session.Controller.CursorState.ShowCursor,
                out localEchoText))
            {
                this.localEcho.RegisterPrintableEcho(localEchoText);
                this.RenderLocalEcho(localEchoText);
            }
        }

        private void SendBackspaceInput(string text)
        {
            this.sendInput(text);
            this.localEcho.NotifyUserInput(text);

            if (this.session.IsAlternateScreenActive)
                return;

            string screenText = this.session.GetScreenText();
            if (this.localEcho.IsPasswordEntryActive(screenText))
            {
                this.localEcho.RegisterPasswordKeySuppressor();
                return;
            }

            string localEchoText;
            if (this.localEcho.TryCreateBackspaceEcho(
                text,
                screenText,
                this.session.Controller.CursorState.ShowCursor,
                out localEchoText))
            {
                this.localEcho.CompleteBackspaceUndo(text);
                this.RenderLocalEcho(localEchoText);
            }
        }

        private void RenderLocalEcho(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            this.session.Push(text);
            this.RenderSessionChanges(TerminalRenderPipeline.ChunkRequiresFullRepaint(text));
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
            this.selectionAnchor = this.ToDocumentPoint(row, col);
            this.selectionEnd = this.selectionAnchor;
            this.lastSelectionMouseLocation = e.Location;
            this.Capture = true;
            this.lastSelectionInvalidateRect = Rectangle.Empty;
            this.InvalidateSelectionRegion();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!this.isSelecting || (e.Button & MouseButtons.Left) == 0)
                return;

            this.lastSelectionMouseLocation = e.Location;
            this.UpdateSelectionEndFromMouse(e.Location);
            this.UpdateSelectionAutoScroll(e.Location);
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (this.isSelecting)
                {
                    this.isSelecting = false;
                    this.Capture = false;
                    this.selectionAutoScrollTimer.Stop();
                    this.UpdateSelectionEndFromMouse(e.Location);

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
            if (this.updatingScrollBar)
                return;

            this.SetViewTopRow(e.NewValue, userInitiated: true);
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                this.ApplyZoomDelta(e.Delta > 0 ? 0.05f : -0.05f);
                return;
            }

            if (this.GetTailTopRow() <= 0)
                return;

            int lineDelta = e.Delta > 0 ? -3 : 3;
            this.SetViewTopRow(this.viewTopRow + lineDelta, userInitiated: true);
        }

        private void ApplyZoomDelta(float delta)
        {
            float newZoom = this.userZoomFactor + delta;
            if (newZoom < TerminalDisplayScale.MinUserZoom)
                newZoom = TerminalDisplayScale.MinUserZoom;
            if (newZoom > TerminalDisplayScale.MaxUserZoom)
                newZoom = TerminalDisplayScale.MaxUserZoom;

            if (Math.Abs(newZoom - this.userZoomFactor) < 0.01f)
                return;

            this.userZoomFactor = newZoom;
            this.InvalidateCellMetrics();
            this.ApplyImmediateLocalResizeRepaint();
            this.SchedulePtyResizeNotification();
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

            int pendingChars = this.GetPendingCharCount();

            if (pendingChars >= CoalescedRenderThresholdChars)
            {
                this.DrainAndRender();
                if ((this.GetPendingCharCount() > 0 || this.deferredPaintRows.Count > 0)
                    && !this.renderTimer.Enabled
                    && !this.IsDisposed)
                {
                    this.renderTimer.Start();
                }

                return;
            }

            if (pendingChars <= ImmediateRenderThresholdChars)
            {
                this.DrainAndRender();
                return;
            }

            this.DrainAndRender();

            if (this.GetPendingCharCount() > 0
                && !this.renderTimer.Enabled
                && !this.IsDisposed)
            {
                this.renderTimer.Start();
            }
        }

        private void OnRenderTimerTick(object sender, EventArgs e)
        {
            int passes = this.IsCatchUpMode() ? CatchUpTimerPasses : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                if (this.deferredPaintRows.Count > 0)
                    this.PaintDeferredRows();

                if (this.GetPendingCharCount() > 0)
                    this.DrainAndRender();
                else if (pass > 0)
                    break;
            }

            if (this.GetPendingCharCount() > 0 || this.deferredPaintRows.Count > 0)
            {
                if (!this.renderTimer.Enabled && !this.IsDisposed)
                    this.renderTimer.Start();
            }
            else
            {
                this.renderTimer.Stop();
            }
        }

        private int GetPendingCharCount()
        {
            lock (this.pendingLock)
            {
                int count = 0;
                foreach (string chunk in this.pendingChunks)
                    count += chunk != null ? chunk.Length : 0;
                return count;
            }
        }

        private void DrainAndRender()
        {
            List<string> chunks = new List<string>();
            int charBudget = this.IsCatchUpMode() ? CatchUpMaxCharsPerDrain : MaxCharsPerDrain;
            int charCount = 0;
            lock (this.pendingLock)
            {
                if (this.pendingChunks.Count == 0 && this.deferredPaintRows.Count == 0)
                {
                    this.renderTimer.Stop();
                    return;
                }

                while (this.pendingChunks.Count > 0 && charCount < charBudget)
                {
                    string chunk = this.pendingChunks.Dequeue();
                    chunks.Add(chunk);
                    charCount += chunk != null ? chunk.Length : 0;
                }
            }

            if (chunks.Count == 0)
            {
                if (this.deferredPaintRows.Count > 0)
                    this.PaintDeferredRows();
                return;
            }

            bool wasFollowingTail = this.followTail || this.IsAtTail();
            bool forceFullRepaint = false;
            foreach (string chunk in chunks)
            {
                if (TerminalRenderPipeline.ChunkRequiresFullRepaint(chunk))
                    forceFullRepaint = true;
                this.session.Push(chunk);
            }

            this.followTail = wasFollowingTail;
            this.RenderSessionChanges(forceFullRepaint);
        }

        private void RenderSessionChanges(bool forceFullRepaint)
        {
            if (!this.session.ConsumeChangedFlag() && this.deferredPaintRows.Count == 0)
                return;

            if (forceFullRepaint)
                this.deferredPaintRows.Clear();

            if (this.IsCatchUpMode())
                forceFullRepaint = true;

            bool alternateScreenActive = this.session.IsAlternateScreenActive;
            if (alternateScreenActive != this.lastAlternateScreenActive)
            {
                forceFullRepaint = true;
                this.deferredPaintRows.Clear();
                this.renderPipeline.InvalidatePreviousGrid();
                this.renderPipeline.InvalidateRowCache();
                this.lastAlternateScreenActive = alternateScreenActive;
            }

            int viewTopBefore = this.viewTopRow;
            this.UpdateScrollRange();

            int scrollDeltaRows = this.session.IsAlternateScreenActive
                ? 0
                : this.viewTopRow - viewTopBefore;
            this.EnsureCellMetrics();
            this.EnsureFrameCacheBitmap();
            if (this.frameCache == null)
                return;

            // Bitmap scroll optimization assumes pure viewport panning. Fast server
            // output rewrites every row, so repaint the full visible grid instead.
            if (scrollDeltaRows != 0)
            {
                this.deferredPaintRows.Clear();
                forceFullRepaint = true;
            }

            IList<int> dirtyRows;
            if (forceFullRepaint)
            {
                this.renderPipeline.RebuildFullFrame(
                    this.frameCache,
                    this.session.Controller,
                    this.GetEffectiveViewTopRow(),
                    this.frameCacheWidth);
                dirtyRows = null;
            }
            else
            {
                var diffOptions = new TerminalRowDiffOptions { ForceFullRepaint = false };
                int renderBudget = this.GetRenderFrameBudget(forceFullRepaint: false);
                dirtyRows = this.renderPipeline.UpdateFrame(
                    this.frameCache,
                    this.session.Controller,
                    this.GetEffectiveViewTopRow(),
                    this.frameCacheWidth,
                    this.frameCacheHeight,
                    diffOptions,
                    renderBudget,
                    this.deferredPaintRows);
            }

            this.InvalidateDirtyRows(dirtyRows, forceFullRepaint);
            if (this.hasSelection)
                this.InvalidateSelectionRegion();

            if (this.deferredPaintRows.Count > 0
                && !this.renderTimer.Enabled
                && !this.IsDisposed)
            {
                this.renderTimer.Start();
            }
        }

        private void PaintDeferredRows()
        {
            this.EnsureFrameCacheBitmap();
            if (this.frameCache == null || this.deferredPaintRows.Count == 0)
                return;

            IList<int> paintedRows = this.renderPipeline.PaintDeferredRows(
                this.frameCache,
                this.session.Controller,
                this.GetEffectiveViewTopRow(),
                this.frameCacheWidth,
                this.GetRenderFrameBudget(forceFullRepaint: false),
                this.deferredPaintRows);

            this.InvalidateDirtyRows(paintedRows, fullRepaint: false);
            if (this.hasSelection)
                this.InvalidateSelectionRegion();
        }

        private void OnTerminalResize(object sender, EventArgs e)
        {
            this.ScheduleLocalResizeRepaint();
            this.SchedulePtyResizeNotification();
        }

        private void OnTerminalLayout(object sender, LayoutEventArgs e)
        {
            this.ScheduleLocalResizeRepaint();
            this.SchedulePtyResizeNotification();
        }

        private void ScheduleLocalResizeRepaint()
        {
            if (!this.IsHandleCreated || this.IsDisposed)
                return;

            if (!this.localResizeCoalesceTimer.Enabled)
                this.localResizeCoalesceTimer.Start();
        }

        private void SchedulePtyResizeNotification()
        {
            this.ptyResizeDebounceTimer.Stop();
            this.ptyResizeDebounceTimer.Start();
        }

        private void OnLocalResizeCoalesceTick(object sender, EventArgs e)
        {
            this.localResizeCoalesceTimer.Stop();
            this.ApplyImmediateLocalResizeRepaint();
        }

        private void ApplyImmediateLocalResizeRepaint()
        {
            this.InvalidateCellMetrics();
            this.EnsureCellMetrics();

            // Full-screen apps redraw after SIGWINCH; avoid reflowing the VT buffer early.
            if (!this.session.IsAlternateScreenActive)
                this.ApplySessionSize(this.Columns, this.Rows);

            this.EnsureFrameCacheBitmap();
            this.renderPipeline.InvalidatePreviousGrid();
            this.RebuildFrameCache();
            this.Invalidate();
        }

        internal void CompletePtyResizeRepaint()
        {
            this.localEcho.ResetPendingEcho();
            this.UpdateScrollRange();
            this.renderPipeline.InvalidatePreviousGrid();
            this.RebuildFrameCache();
            this.Invalidate();
        }

        private void InvalidateCellMetrics()
        {
            this.cachedMetricsWidth = -1;
            this.cachedMetricsHeight = -1;
            this.cachedDisplayPointSize = -1f;
            this.cachedDpiScale = -1f;
        }

        private void OnPtyResizeDebounceTick(object sender, EventArgs e)
        {
            this.ptyResizeDebounceTimer.Stop();

            if (this.TerminalResized != null)
                this.TerminalResized(this, EventArgs.Empty);
        }

        private void EnsureCellMetrics()
        {
            int width = this.GetPaintableWidth();
            int height = this.DisplayRectangle.Height;
            float dpiScale = this.GetDpiScale();
            float pointSize = this.ComputeFontPointSize();
            if (width == this.cachedMetricsWidth
                && height == this.cachedMetricsHeight
                && Math.Abs(pointSize - this.cachedDisplayPointSize) < 0.05f
                && Math.Abs(dpiScale - this.cachedDpiScale) < 0.01f
                && this.cellWidth > 0
                && this.cellHeight > 0)
            {
                return;
            }

            this.RefreshCellMetrics();
            this.cachedMetricsWidth = width;
            this.cachedMetricsHeight = height;
            this.cachedDisplayPointSize = pointSize;
            this.cachedDpiScale = dpiScale;
        }

        internal void SyncSessionGeometry(bool force = false)
        {
            this.EnsureCellMetrics();
            this.ApplySessionSize(this.Columns, this.Rows, force);
        }

        private void RefreshCellMetrics()
        {
            float pointSize = this.ComputeFontPointSize();
            this.renderPipeline.UpdateDisplayScale(pointSize);
            this.cellWidth = this.renderPipeline.CellWidth;
            this.cellHeight = this.renderPipeline.CellHeight;
        }

        private float ComputeFontPointSize()
        {
            return TerminalDisplayScale.ComputePointSize(
                this.GetDpiScale(),
                this.GetPaintableWidth(),
                Math.Max(1, this.DisplayRectangle.Height),
                this.userZoomFactor);
        }

        private float GetDpiScale()
        {
            if (!this.IsHandleCreated)
                return 1f;

            using (Graphics graphics = this.CreateGraphics())
                return graphics.DpiX / 96f;
        }

        private int GetEffectiveViewTopRow()
        {
            if (this.session.IsAlternateScreenActive)
                return 0;

            return this.viewTopRow;
        }

        private bool IsCatchUpMode()
        {
            return this.GetPendingCharCount() >= CatchUpPendingChars
                || this.deferredPaintRows.Count >= Math.Max(8, this.Rows);
        }

        private int GetRenderFrameBudget(bool forceFullRepaint)
        {
            if (forceFullRepaint || this.session.IsAlternateScreenActive)
                return int.MaxValue;

            int pending = this.GetPendingCharCount();
            int deferred = this.deferredPaintRows.Count;
            int visibleRows = Math.Max(1, this.Rows);

            if (pending >= AggressiveCatchUpPendingChars || deferred >= visibleRows * 2)
                return int.MaxValue;
            if (this.IsCatchUpMode())
                return Math.Max(RenderFrameBudgetRows, visibleRows);

            return RenderFrameBudgetRows;
        }

        private int GetPaintableWidth()
        {
            // DisplayRectangle already excludes the docked scrollbar.
            return Math.Max(0, this.DisplayRectangle.Width);
        }

        private void InvalidateDirtyRows(System.Collections.Generic.IList<int> dirtyRows, bool fullRepaint)
        {
            if (fullRepaint
                || this.IsCatchUpMode()
                || dirtyRows == null
                || dirtyRows.Count == 0)
            {
                this.Invalidate(this.GetTerminalContentBounds());
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
            this.EnsureCellMetrics();
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

            this.frameCache = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            this.frameCacheWidth = width;
            this.frameCacheHeight = height;
            this.renderPipeline.InvalidatePreviousGrid();
        }

        private void RebuildFrameCache()
        {
            this.EnsureCellMetrics();
            this.EnsureFrameCacheBitmap();
            if (this.frameCache == null)
                return;

            this.deferredPaintRows.Clear();
            this.renderPipeline.RebuildFullFrame(
                this.frameCache,
                this.session.Controller,
                this.GetEffectiveViewTopRow(),
                this.frameCacheWidth);
        }

        private void ApplyViewportScroll(int scrollDeltaRows)
        {
            if (scrollDeltaRows == 0 || this.session.IsAlternateScreenActive)
                return;

            this.EnsureFrameCacheBitmap();
            if (this.frameCache == null)
                return;

            if (!this.renderPipeline.TryScrollFrame(
                this.frameCache,
                this.session.Controller,
                this.GetEffectiveViewTopRow(),
                scrollDeltaRows,
                this.frameCacheWidth))
            {
                this.renderPipeline.RebuildFullFrame(
                    this.frameCache,
                    this.session.Controller,
                    this.GetEffectiveViewTopRow(),
                    this.frameCacheWidth);
            }

            this.Invalidate(this.GetTerminalContentBounds());
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
                string text = TerminalTextSelection.ExtractTextFromDocumentRange(
                    this.session.Controller,
                    Math.Max(1, this.Columns),
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
            if (!this.hasSelection || graphics == null || this.frameCache == null)
                return;

            TerminalCellPoint visibleAnchor;
            TerminalCellPoint visibleEnd;
            if (!this.TryGetVisibleSelection(out visibleAnchor, out visibleEnd))
                return;

            TerminalCellGrid grid = this.renderPipeline.LastRenderedGrid;
            if (grid == null)
            {
                grid = TerminalCellGridBuilder.Build(
                    this.session.Controller,
                    this.GetEffectiveViewTopRow(),
                    Math.Max(1, this.Rows),
                    Math.Max(1, this.Columns));
            }

            Rectangle bounds = this.GetVisibleSelectionPixelBounds();
            if (bounds.IsEmpty)
                return;

            using (var composed = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb))
            {
                using (var memory = Graphics.FromImage(composed))
                    memory.DrawImage(this.frameCache, new Rectangle(0, 0, bounds.Width, bounds.Height), bounds, GraphicsUnit.Pixel);

                this.renderPipeline.PaintSelection(
                    composed,
                    grid,
                    visibleAnchor,
                    visibleEnd,
                    new Point(-bounds.X, -bounds.Y));

                graphics.DrawImage(composed, bounds.Location);
            }
        }

        private void InvalidateSelectionRegion()
        {
            if (!this.IsHandleCreated)
                return;

            Rectangle newRect = Rectangle.Empty;
            if (this.hasSelection)
            {
                this.EnsureCellMetrics();
                newRect = this.GetVisibleSelectionPixelBounds();
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
            {
                if (!this.lastSelectionInvalidateRect.IsEmpty)
                {
                    this.Invalidate(this.lastSelectionInvalidateRect);
                    this.lastSelectionInvalidateRect = Rectangle.Empty;
                }

                return;
            }

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
            this.selectionAutoScrollTimer.Stop();
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

        private void UpdateSelectionEndFromMouse(Point location)
        {
            if (!this.TryHitTestCellForSelection(location.X, location.Y, out int row, out int col))
                return;

            TerminalCellPoint end = this.ToDocumentPoint(row, col);
            if (end.Row == this.selectionEnd.Row && end.Column == this.selectionEnd.Column)
                return;

            this.selectionEnd = end;
            this.InvalidateSelectionRegion();
        }

        private void UpdateSelectionAutoScroll(Point location)
        {
            if (!this.isSelecting)
            {
                this.selectionAutoScrollTimer.Stop();
                return;
            }

            int clientHeight = this.DisplayRectangle.Height;
            if ((location.Y < 0 && this.viewTopRow > 0)
                || (location.Y >= clientHeight && this.viewTopRow < this.GetTailTopRow()))
            {
                if (!this.selectionAutoScrollTimer.Enabled)
                    this.selectionAutoScrollTimer.Start();
                return;
            }

            this.selectionAutoScrollTimer.Stop();
        }

        private void OnSelectionAutoScrollTimerTick(object sender, EventArgs e)
        {
            if (!this.isSelecting)
            {
                this.selectionAutoScrollTimer.Stop();
                return;
            }

            int clientHeight = this.DisplayRectangle.Height;
            int delta = 0;
            if (this.lastSelectionMouseLocation.Y < 0)
                delta = -SelectionAutoScrollRows;
            else if (this.lastSelectionMouseLocation.Y >= clientHeight)
                delta = SelectionAutoScrollRows;

            if (delta == 0)
            {
                this.selectionAutoScrollTimer.Stop();
                return;
            }

            int previousTop = this.viewTopRow;
            this.SetViewTopRow(this.viewTopRow + delta, userInitiated: true);
            if (previousTop == this.viewTopRow)
            {
                this.selectionAutoScrollTimer.Stop();
                return;
            }

            this.UpdateSelectionEndFromMouse(this.lastSelectionMouseLocation);
        }

        private TerminalCellPoint ToDocumentPoint(int visibleRow, int column)
        {
            return new TerminalCellPoint(this.viewTopRow + visibleRow, column);
        }

        private bool TryGetVisibleSelection(out TerminalCellPoint visibleAnchor, out TerminalCellPoint visibleEnd)
        {
            visibleAnchor = new TerminalCellPoint();
            visibleEnd = new TerminalCellPoint();
            if (!this.hasSelection)
                return false;

            TerminalTextSelection.OrderSelectionPoints(
                this.selectionAnchor,
                this.selectionEnd,
                out TerminalCellPoint start,
                out TerminalCellPoint stop);

            int visibleTop = this.viewTopRow;
            int visibleBottom = this.viewTopRow + Math.Max(1, this.Rows) - 1;
            int clippedStartRow = Math.Max(start.Row, visibleTop);
            int clippedStopRow = Math.Min(stop.Row, visibleBottom);
            if (clippedStopRow < clippedStartRow)
                return false;

            int columns = Math.Max(1, this.Columns);
            int startColumn = clippedStartRow == start.Row ? start.Column : 0;
            int stopColumn = clippedStopRow == stop.Row ? stop.Column : columns - 1;
            visibleAnchor = new TerminalCellPoint(clippedStartRow - visibleTop, startColumn);
            visibleEnd = new TerminalCellPoint(clippedStopRow - visibleTop, stopColumn);
            return true;
        }

        private Rectangle GetVisibleSelectionPixelBounds()
        {
            TerminalCellPoint visibleAnchor;
            TerminalCellPoint visibleEnd;
            if (!this.TryGetVisibleSelection(out visibleAnchor, out visibleEnd))
                return Rectangle.Empty;

            return TerminalTextSelection.GetSelectionPixelBounds(
                visibleAnchor,
                visibleEnd,
                Math.Max(1, this.Columns),
                this.cellWidth,
                this.cellHeight);
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
                string screenText = this.session.GetScreenText();
                bool passwordEntry = this.localEcho.IsPasswordEntryActive(screenText);
                if (!passwordEntry)
                    this.localEcho.ResetPendingEcho();

                this.sendInput(text);
                if (text.IndexOf('\n') >= 0)
                    this.localEcho.NotifyUserInput("\r");

                if (passwordEntry)
                {
                    foreach (char character in text)
                    {
                        if (char.IsControl(character) && character != '\b' && character != '\x7f')
                            continue;

                        this.localEcho.RegisterPasswordKeySuppressor();
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
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
            if (this.session.IsAlternateScreenActive)
            {
                this.viewTopRow = 0;
                this.scrollBar.Visible = false;
                return;
            }

            int tailTop = this.GetTailTopRow();
            int largeChange = Math.Max(1, this.Rows);
            int maximum = Math.Max(largeChange - 1, tailTop + largeChange - 1);
            this.updatingScrollBar = true;
            try
            {
                if (this.scrollBar.Value > maximum)
                    this.scrollBar.Value = maximum;
            }
            finally
            {
                this.updatingScrollBar = false;
            }

            this.scrollBar.SmallChange = 1;
            this.scrollBar.LargeChange = largeChange;
            this.scrollBar.Maximum = maximum;
            this.scrollBar.Visible = tailTop > 0;

            if (this.followTail)
            {
                this.viewTopRow = tailTop;
            }
            else
            {
                this.viewTopRow = ClampViewTopRow(this.viewTopRow, tailTop);
            }

            this.UpdateScrollBarValue();
        }

        private int GetTailTopRow()
        {
            return Math.Max(0, this.session.Controller.ViewPort.TopRow);
        }

        internal static int ClampViewTopRow(int requestedTopRow, int tailTopRow)
        {
            if (tailTopRow < 0)
                tailTopRow = 0;
            if (requestedTopRow < 0)
                return 0;
            if (requestedTopRow > tailTopRow)
                return tailTopRow;
            return requestedTopRow;
        }

        internal static bool ShouldFollowTailAfterScroll(int viewTopRow, int tailTopRow)
        {
            return ClampViewTopRow(viewTopRow, tailTopRow) >= Math.Max(0, tailTopRow);
        }

        private bool IsAtTail()
        {
            return ShouldFollowTailAfterScroll(this.viewTopRow, this.GetTailTopRow());
        }

        private void SetViewTopRow(int requestedTopRow, bool userInitiated)
        {
            if (this.session.IsAlternateScreenActive)
                return;

            int tailTop = this.GetTailTopRow();
            int newTop = ClampViewTopRow(requestedTopRow, tailTop);
            int rowDelta = newTop - this.viewTopRow;
            if (userInitiated)
                this.followTail = ShouldFollowTailAfterScroll(newTop, tailTop);

            if (rowDelta == 0)
            {
                this.UpdateScrollBarValue();
                return;
            }

            this.viewTopRow = newTop;
            this.UpdateScrollBarValue();
            this.ApplyViewportScroll(rowDelta);
        }

        private void UpdateScrollBarValue()
        {
            int value = ClampViewTopRow(this.viewTopRow, this.GetTailTopRow());
            value = Math.Min(this.scrollBar.Maximum, Math.Max(this.scrollBar.Minimum, value));
            if (this.scrollBar.Value == value)
                return;

            this.updatingScrollBar = true;
            try
            {
                this.scrollBar.Value = value;
            }
            finally
            {
                this.updatingScrollBar = false;
            }
        }

        private Rectangle GetTerminalContentBounds()
        {
            int width = this.frameCacheWidth > 0 ? this.frameCacheWidth : this.GetPaintableWidth();
            return new Rectangle(0, 0, Math.Max(0, width), Math.Max(1, this.DisplayRectangle.Height));
        }

        private void PaintCaretOverlay(Graphics graphics)
        {
            var controller = this.session.Controller;
            if (!controller.CursorState.ShowCursor)
                return;

            TextPosition cursor = controller.ViewPort.CursorPosition;
            int row = cursor.Row + (controller.ViewPort.TopRow - this.GetEffectiveViewTopRow());
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
            int row = cursor.Row + (controller.ViewPort.TopRow - this.GetEffectiveViewTopRow());
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
