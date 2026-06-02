using System;
using System.Drawing;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet
{
    internal class SshTerminalControl : RichTextBox
    {
        private readonly Action<string> sendInput;
        private readonly AnsiTerminalScreen screen = new AnsiTerminalScreen();
        private bool renderDirty;

        internal int Columns
        {
            get
            {
                int charWidth = TextRenderer.MeasureText("W", this.Font).Width;
                if (charWidth <= 0)
                    return 80;
                return Math.Max(20, this.ClientSize.Width / charWidth);
            }
        }

        internal int Rows
        {
            get
            {
                int charHeight = this.Font.Height;
                if (charHeight <= 0)
                    return 24;
                return Math.Max(8, this.ClientSize.Height / charHeight);
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
            this.Font = new Font(FontFamily.GenericMonospace, 10f, FontStyle.Regular);
            this.ReadOnly = true;
            this.HideSelection = false;
            this.Multiline = true;
            this.WordWrap = false;
            this.ScrollBars = RichTextBoxScrollBars.Both;
            this.Resize += this.OnTerminalResize;
        }

        internal void AppendAnsi(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(this.AppendAnsi), text);
                return;
            }

            this.screen.Feed(text);
            this.renderDirty = true;

            if (this.InvokeRequired)
                this.BeginInvoke(new Action(this.RenderScreen));
            else
                this.RenderScreen();
        }

        private void RenderScreen()
        {
            if (!this.renderDirty)
                return;

            this.renderDirty = false;
            this.screen.RenderTo(this, this.Font);

            if (this.renderDirty)
                this.BeginInvoke(new Action(this.RenderScreen));
        }

        private void OnTerminalResize(object sender, EventArgs e)
        {
            if (this.TerminalResized != null)
                this.TerminalResized(this, EventArgs.Empty);
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
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            this.sendInput(e.KeyChar.ToString());
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

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
            }

            if (!string.IsNullOrEmpty(controlSequence))
            {
                this.sendInput(controlSequence);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }
    }
}
