using System;
using System.Text;
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
        private SshNetConnectionSetup connectionSetup;
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
            this.terminalControl.TerminalResized += this.OnTerminalResized;
            this.Controls.Add(this.terminalControl);
            this.GrabInput = true;
        }

        public override bool Connect()
        {
            try
            {
                var sshOptions = this.Favorite.ProtocolProperties as SshOptions;
                IGuardedSecurity credentials = this.ResolveFavoriteCredentials();

                string error;
                if (!SshNetConnectionInfoFactory.TryCreate(
                    this.Favorite.ServerName,
                    this.Favorite.Port,
                    credentials,
                    sshOptions,
                    out this.connectionSetup,
                    out error))
                {
                    this.LastError = error;
                    return false;
                }

                this.sshClient = new SshClient(this.connectionSetup.ConnectionInfo);
                SshNetSessionConfigurator.AttachHostKeyHandler(this.sshClient);
                this.sshClient.ErrorOccurred += this.SshClientOnErrorOccurred;
                this.sshClient.Connect();

                if (!this.sshClient.IsConnected)
                {
                    this.LastError = "Unable to connect over SSH.";
                    return false;
                }

                SshNetSessionConfigurator.ApplyPostConnectFeatures(this.sshClient, this.connectionSetup);

                this.shellStream = this.sshClient.CreateShellStream(
                    "xterm",
                    (uint)this.terminalControl.Columns,
                    (uint)this.terminalControl.Rows,
                    0,
                    0,
                    1024);

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

        private void OnTerminalResized(object sender, EventArgs e)
        {
            if (!this.Connected)
                return;

            SshNetSessionConfigurator.TryResizePty(
                this.shellStream,
                (uint)this.terminalControl.Columns,
                (uint)this.terminalControl.Rows);
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
}
