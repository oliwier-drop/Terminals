using System;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet
{
    internal class SshTerminalControl : RichTextBox
    {
        private const int RenderIntervalMs = 50;
        private const int ScrollbackExtraLines = 50;
        private const int ResizeDebounceMs = 200;
        private const int MaxColumns = 260;
        private const int MaxRows = 100;

        private readonly Action<string> sendInput;
        private readonly AnsiTerminalScreen screen = new AnsiTerminalScreen();
        private readonly StringBuilder pendingOutput = new StringBuilder();
        private readonly object pendingLock = new object();
        private readonly Timer renderTimer;
        private readonly Timer resizeDebounceTimer;
        private bool renderDirty;
        private bool pendingBeforeHandle;

        internal int Columns
        {
            get
            {
                int charWidth = TextRenderer.MeasureText("W", this.Font).Width;
                if (charWidth <= 0)
                    return 80;
                return Math.Min(MaxColumns, Math.Max(20, this.ClientSize.Width / charWidth));
            }
        }

        internal int Rows
        {
            get
            {
                int charHeight = this.Font.Height;
                if (charHeight <= 0)
                    return 24;
                return Math.Min(MaxRows, Math.Max(8, this.ClientSize.Height / charHeight));
            }
        }

        internal event EventHandler TerminalResized;

        internal SshTerminalControl(Action<string> sendInput)
        {
            this.sendInput = sendInput;
            this.Dock = DockStyle.Fill;
            this.BorderStyle = BorderStyle.None;
            this.BackColor = Color.Black;
            this.ForeColor = Color.Gainsboro;
            this.Font = CreateTerminalFont();
            this.ReadOnly = true;
            this.HideSelection = false;
            this.Multiline = true;
            this.WordWrap = false;
            this.ScrollBars = RichTextBoxScrollBars.Both;
            this.TabStop = true;
            this.EnableDoubleBuffering();

            this.renderTimer = new Timer { Interval = RenderIntervalMs };
            this.renderTimer.Tick += this.OnRenderTimerTick;

            this.resizeDebounceTimer = new Timer { Interval = ResizeDebounceMs };
            this.resizeDebounceTimer.Tick += this.OnResizeDebounceTick;

            this.Resize += this.OnTerminalResize;
            this.HandleCreated += this.OnHandleCreated;
            this.GotFocus += this.OnGotFocus;
            this.Click += this.OnClick;
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

        /// <summary>Queues ANSI text and paints immediately (connect banner, session end).</summary>
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.renderTimer.Stop();
                this.renderTimer.Dispose();
                this.resizeDebounceTimer.Stop();
                this.resizeDebounceTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnHandleCreated(object sender, EventArgs e)
        {
            if (this.pendingBeforeHandle)
            {
                this.pendingBeforeHandle = false;
                this.ScheduleRender();
            }
        }

        private void OnGotFocus(object sender, EventArgs e)
        {
            this.ScrollToCaret();
        }

        private void OnClick(object sender, EventArgs e)
        {
            this.Focus();
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

            this.SyncScreenGeometry();
            this.screen.Feed(chunk);
            this.renderDirty = true;
            this.RenderScreen();
        }

        internal void InvalidateRenderCache()
        {
            this.screen.ResetRenderCache();
        }

        private void RenderScreen()
        {
            if (!this.renderDirty)
                return;

            this.renderDirty = false;
            int maxLines = Math.Min(500, this.Rows + ScrollbackExtraLines);
            int caretIndex;
            this.screen.RenderPlainTo(this, maxLines, out caretIndex);
        }

        private void OnTerminalResize(object sender, EventArgs e)
        {
            this.resizeDebounceTimer.Stop();
            this.resizeDebounceTimer.Start();
        }

        private void OnResizeDebounceTick(object sender, EventArgs e)
        {
            this.resizeDebounceTimer.Stop();
            this.SyncScreenGeometry();
            this.screen.ResetRenderCache();
            this.renderDirty = true;
            this.RenderScreen();
            if (this.TerminalResized != null)
                this.TerminalResized(this, EventArgs.Empty);
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

        private void SyncScreenGeometry()
        {
            this.screen.TerminalWidth = Math.Max(20, this.Columns);
            this.screen.TerminalHeight = Math.Max(8, this.Rows);
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
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar))
                return;

            this.sendInput(e.KeyChar.ToString());
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            string controlSequence = null;
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    controlSequence = "\r";
                    break;
                case Keys.Back:
                    controlSequence = "\b";
                    break;
                case Keys.Tab:
                    controlSequence = "\t";
                    break;
                case Keys.Left:
                    controlSequence = "\x1B[D";
                    break;
                case Keys.Right:
                    controlSequence = "\x1B[C";
                    break;
                case Keys.Up:
                    controlSequence = "\x1B[A";
                    break;
                case Keys.Down:
                    controlSequence = "\x1B[B";
                    break;
                case Keys.Escape:
                    controlSequence = "\x1B";
                    break;
            }

            if (!string.IsNullOrEmpty(controlSequence))
            {
                this.sendInput(controlSequence);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
    }
}
