using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Common;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Plugins.Putty;

namespace Terminals.Plugins.SshNet
{
    internal class SshNetConnection : Connection, IFocusable, IHandleKeyboardInput
    {
        private readonly SshTerminalControl terminalControl;
        private readonly Encoding streamEncoding = Encoding.UTF8;
        private readonly object writeLock = new object();

        private SshClient sshClient;
        private ShellStream shellStream;
        private CancellationTokenSource readCancellation;
        private Task readTask;

        public bool GrabInput { get; set; }

        public override bool Connected
        {
            get
            {
                return this.sshClient != null && this.sshClient.IsConnected && this.shellStream != null;
            }
        }

        public SshNetConnection()
        {
            this.Dock = DockStyle.Fill;
            this.terminalControl = new SshTerminalControl(this.SendInput);
            this.Controls.Add(this.terminalControl);
            this.GrabInput = true;
        }

        public override bool Connect()
        {
            try
            {
                ConnectionInfo info = this.CreateConnectionInfo();
                this.sshClient = new SshClient(info);
                this.sshClient.ErrorOccurred += this.SshClientOnErrorOccurred;
                this.sshClient.Connect();

                if (!this.sshClient.IsConnected)
                {
                    this.LastError = "Unable to connect over SSH.";
                    return false;
                }

                this.shellStream = this.sshClient.CreateShellStream("xterm", this.terminalControl.Columns, this.terminalControl.Rows, 0, 0, 1024);
                this.StartReadLoop();
                return true;
            }
            catch (Exception exception)
            {
                this.LastError = exception.Message;
                Logging.Error("SSH.NET connect failed.", exception);
                return false;
            }
        }

        private ConnectionInfo CreateConnectionInfo()
        {
            var sshOptions = this.Favorite.ProtocolProperties as SshOptions;
            IGuardedSecurity credentials = this.ResolveFavoriteCredentials();
            string userName = credentials.UserName ?? string.Empty;
            string password = credentials.Password;

            var methods = new List<AuthenticationMethod>();
            if (!string.IsNullOrEmpty(password))
                methods.Add(new PasswordAuthenticationMethod(userName, password));
            else
                methods.Add(new NoneAuthenticationMethod(userName));

            var info = new ConnectionInfo(this.Favorite.ServerName, this.Favorite.Port, userName, methods.ToArray());
            if (sshOptions != null && sshOptions.EnableCompression)
                info.EnableCompression = true;

            return info;
        }

        private void StartReadLoop()
        {
            this.readCancellation = new CancellationTokenSource();
            CancellationToken token = this.readCancellation.Token;
            this.readTask = Task.Factory.StartNew(() => this.ReadLoop(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void ReadLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            try
            {
                while (!cancellationToken.IsCancellationRequested && this.shellStream != null && this.sshClient != null && this.sshClient.IsConnected)
                {
                    if (!this.shellStream.DataAvailable)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    int read = this.shellStream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        continue;

                    string text = this.streamEncoding.GetString(buffer, 0, read);
                    this.terminalControl.AppendAnsi(text);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                Logging.Error("SSH.NET stream read failed.", exception);
                this.SafeAppend(string.Format("\r\n[SSH.NET read error] {0}\r\n", exception.Message));
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    this.FireDisconnected();
            }
        }

        private void SendInput(string text)
        {
            if (string.IsNullOrEmpty(text) || !this.Connected)
                return;

            try
            {
                lock (this.writeLock)
                {
                    this.shellStream.Write(text);
                    this.shellStream.Flush();
                }
            }
            catch (Exception exception)
            {
                Logging.Error("SSH.NET stream write failed.", exception);
            }
        }

        private void SshClientOnErrorOccurred(object sender, ExceptionEventArgs e)
        {
            Logging.Error("SSH.NET client error.", e.Exception);
            this.SafeAppend(string.Format("\r\n[SSH.NET error] {0}\r\n", e.Exception.Message));
        }

        private void SafeAppend(string text)
        {
            if (this.IsDisposed)
                return;

            if (this.InvokeRequired)
                this.BeginInvoke(new Action<string>(this.SafeAppend), text);
            else
                this.terminalControl.AppendAnsi(text);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.readCancellation != null)
                {
                    this.readCancellation.Cancel();
                    this.readCancellation.Dispose();
                    this.readCancellation = null;
                }

                if (this.shellStream != null)
                {
                    this.shellStream.Dispose();
                    this.shellStream = null;
                }

                if (this.sshClient != null)
                {
                    if (this.sshClient.IsConnected)
                        this.sshClient.Disconnect();
                    this.sshClient.Dispose();
                    this.sshClient = null;
                }
            }

            base.Dispose(disposing);
        }

        bool IFocusable.ContainsFocus
        {
            get { return this.terminalControl.ContainsFocus; }
        }

        void IFocusable.Focus()
        {
            this.terminalControl.Focus();
        }
    }

    internal class SshTerminalControl : RichTextBox
    {
        private static readonly Regex AnsiRegex = new Regex(@"\x1B\[[0-9;]*m", RegexOptions.Compiled);
        private readonly Action<string> sendInput;

        private Color currentForeColor = Color.Gainsboro;
        private Color currentBackColor = Color.Black;
        private bool bold;

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

            int idx = 0;
            MatchCollection matches = AnsiRegex.Matches(text);
            foreach (Match match in matches)
            {
                if (match.Index > idx)
                {
                    string chunk = text.Substring(idx, match.Index - idx);
                    this.AppendWithStyle(chunk);
                }

                this.ApplyStyle(match.Value);
                idx = match.Index + match.Length;
            }

            if (idx < text.Length)
                this.AppendWithStyle(text.Substring(idx));

            this.SelectionStart = this.TextLength;
            this.ScrollToCaret();
        }

        private void AppendWithStyle(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return;

            this.SelectionStart = this.TextLength;
            this.SelectionLength = 0;
            this.SelectionColor = this.currentForeColor;
            this.SelectionBackColor = this.currentBackColor;
            this.SelectionFont = new Font(this.Font, this.bold ? FontStyle.Bold : FontStyle.Regular);
            this.AppendText(chunk);
        }

        private void ApplyStyle(string escapeSequence)
        {
            if (string.IsNullOrEmpty(escapeSequence) || escapeSequence.Length < 3)
                return;

            string parameters = escapeSequence.Substring(2, escapeSequence.Length - 3);
            if (string.IsNullOrEmpty(parameters))
            {
                this.ResetStyle();
                return;
            }

            string[] parts = parameters.Split(';');
            foreach (string part in parts)
            {
                int code;
                if (!int.TryParse(part, out code))
                    continue;

                switch (code)
                {
                    case 0:
                        this.ResetStyle();
                        break;
                    case 1:
                        this.bold = true;
                        break;
                    case 22:
                        this.bold = false;
                        break;
                    case 30: this.currentForeColor = Color.Black; break;
                    case 31: this.currentForeColor = Color.IndianRed; break;
                    case 32: this.currentForeColor = Color.LightGreen; break;
                    case 33: this.currentForeColor = Color.Khaki; break;
                    case 34: this.currentForeColor = Color.LightSkyBlue; break;
                    case 35: this.currentForeColor = Color.Plum; break;
                    case 36: this.currentForeColor = Color.MediumTurquoise; break;
                    case 37: this.currentForeColor = Color.Gainsboro; break;
                    case 39: this.currentForeColor = Color.Gainsboro; break;
                    case 40: this.currentBackColor = Color.Black; break;
                    case 41: this.currentBackColor = Color.DarkRed; break;
                    case 42: this.currentBackColor = Color.DarkGreen; break;
                    case 43: this.currentBackColor = Color.Olive; break;
                    case 44: this.currentBackColor = Color.DarkBlue; break;
                    case 45: this.currentBackColor = Color.DarkMagenta; break;
                    case 46: this.currentBackColor = Color.DarkCyan; break;
                    case 47: this.currentBackColor = Color.LightGray; break;
                    case 49: this.currentBackColor = Color.Black; break;
                }
            }
        }

        private void ResetStyle()
        {
            this.bold = false;
            this.currentForeColor = Color.Gainsboro;
            this.currentBackColor = Color.Black;
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
