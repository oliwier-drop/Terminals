// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Common;
using Terminals.Common.Configuration;
using Terminals.Common.Connections;
using Terminals.Configuration;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Plugins.Putty;

namespace Terminals.Plugins.SshNet
{
    internal class SshNetConnection : Connection, IDeferredConnection, IPostConnectTerminalSync, IConnectionExtra, IHandleKeyboardInput, ISettingsConsumer, ICredentialPromptConsumer
    {
        private const int ReadTaskJoinTimeoutMs = 3000;

        private readonly SshTerminalControl terminalControl;
        private readonly Encoding streamEncoding = Encoding.UTF8;
        private readonly object writeLock = new object();
        private readonly object uiOutputLock = new object();
        private readonly StringBuilder uiOutputBatch = new StringBuilder();
        private readonly SshKnownHostsStore knownHosts = SshKnownHostsStore.CreateDefault();
        private int uiOutputFlushScheduled;

        private SshClient sshClient;
        private ShellStream shellStream;
        private SshNetConnectionSetup connectionSetup;
        private CancellationTokenSource readCancellation;
        private Task readTask;
        private const uint MaxTerminalColumns = 260;
        private const uint MaxTerminalRows = 100;

        private int lastPtyColumns = -1;
        private int lastPtyRows = -1;

        private int connectGeneration;
        private int sessionGeneration;
        private volatile bool isConnecting;
        private volatile bool readLoopPendingStart;
        private volatile bool readLoopHadInitialOutput;
        private string pendingInitialShellText;
        private SshSessionCredentials sessionCredentials;

        public IConnectionSettings Settings { get; set; }

        public ICredentialPromptService CredentialPromptService { get; set; }

        public bool GrabInput { get; set; }

        internal bool IsConnecting
        {
            get { return this.isConnecting; }
        }

        public bool IsConnectInProgress
        {
            get { return this.isConnecting; }
        }

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

        public void BeginConnect(Action<bool> completed)
        {
            if (completed == null)
                throw new ArgumentNullException("completed");

            if (this.isConnecting)
                return;

            if (this.Connected)
            {
                completed(true);
                return;
            }

            int generation = Interlocked.Increment(ref this.connectGeneration);
            this.isConnecting = true;
            this.terminalControl.AppendAnsi("\r\nConnecting...\r\n");

            string credentialError;
            if (!this.TryPrepareSessionCredentials(out credentialError))
            {
                this.isConnecting = false;
                this.LastError = credentialError;
                completed(false);
                return;
            }

            Task.Factory.StartNew(
                () =>
                {
                    bool success = false;
                    try
                    {
                        success = this.ConnectCore();
                    }
                    catch (Exception exception)
                    {
                        this.LastError = exception.Message;
                        Logging.Error("SSH.NET connect failed.", exception);
                        success = false;
                    }

                    if (generation != this.connectGeneration)
                        return;

                    this.BeginInvoke(new Action(() =>
                    {
                        this.isConnecting = false;

                        if (generation != this.connectGeneration)
                            return;

                        if (success)
                        {
                            string initial = this.pendingInitialShellText;
                            this.pendingInitialShellText = null;
                            if (!string.IsNullOrEmpty(initial))
                                this.terminalControl.AppendAnsiAndFlush(initial);
                            else
                                this.terminalControl.FlushPendingOutput();

                            this.terminalControl.FocusTerminal();
                            this.Update();
                            this.SchedulePostConnectTerminalSync();

                            if (this.readLoopPendingStart)
                            {
                                this.readLoopPendingStart = false;
                                this.StartReadLoop(this.sessionGeneration, this.readLoopHadInitialOutput);
                            }
                        }

                        completed(success);
                    }));
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
        }

        public override bool Connect()
        {
            bool success = false;
            using (var completed = new ManualResetEvent(false))
            {
                this.BeginConnect(result =>
                {
                    success = result;
                    completed.Set();
                });
                completed.WaitOne();
            }

            return success;
        }

        private bool TryPrepareSessionCredentials(out string error)
        {
            error = null;
            this.sessionCredentials = null;

            var sshOptions = this.Favorite.ProtocolProperties as SshOptions;
            IGuardedSecurity resolved = this.ResolveFavoriteCredentials();
            KeysSection sshKeys = this.Settings != null ? this.Settings.SSHKeys : null;
            IWin32Window owner = this.GetDialogOwner();

            return SshCredentialGate.TryPrepareSessionCredentials(
                resolved,
                sshOptions,
                sshKeys,
                this.Favorite.ServerName,
                this.CredentialPromptService,
                owner,
                out this.sessionCredentials,
                out error);
        }

        private bool ConnectCore()
        {
            this.CleanupSessionForReconnect();

            if (this.sessionCredentials == null)
            {
                this.LastError = "SSH credentials were not prepared.";
                return false;
            }

            var sshOptions = this.Favorite.ProtocolProperties as SshOptions;
            KeysSection sshKeys = this.Settings != null ? this.Settings.SSHKeys : null;
            IWin32Window owner = this.GetDialogOwner();

            string error;
            if (!SshNetConnectionInfoFactory.TryCreate(
                this.Favorite.ServerName,
                this.Favorite.Port,
                this.sessionCredentials,
                sshOptions,
                sshKeys,
                owner,
                out this.connectionSetup,
                out error))
            {
                this.LastError = error;
                return false;
            }

            Logging.Info(string.Format(
                "SSH: connecting to {0}:{1} as {2}.",
                this.Favorite.ServerName,
                this.Favorite.Port,
                this.sessionCredentials.UserName));

            this.sshClient = new SshClient(this.connectionSetup.ConnectionInfo);
            var hostKeyVerifier = new SshHostKeyVerifier(
                this.connectionSetup.Host,
                this.connectionSetup.Port,
                this.knownHosts,
                owner);
            hostKeyVerifier.Attach(this.sshClient);

            this.sshClient.ErrorOccurred += this.SshClientOnErrorOccurred;
            try
            {
                this.ConnectSshClientOnUiThread(owner);
            }
            catch (SshAuthenticationException exception)
            {
                this.LastError = "SSH authentication failed. Check username and password.\r\n" + exception.Message;
                Logging.Error("SSH.NET authentication failed.", exception);
                this.CleanupSessionForReconnect();
                return false;
            }
            catch (SshConnectionException exception)
            {
                this.LastError = "SSH connection failed.\r\n" + exception.Message;
                Logging.Error("SSH.NET connection failed.", exception);
                this.CleanupSessionForReconnect();
                return false;
            }

            if (!this.sshClient.IsConnected)
            {
                this.LastError = "Unable to connect over SSH.";
                this.CleanupSessionForReconnect();
                return false;
            }

            SshNetSessionConfigurator.ApplyPostConnectFeatures(this.sshClient, this.connectionSetup);

            uint columns;
            uint rows;
            uint widthPixels;
            uint heightPixels;
            this.GetShellGeometryOnUiThread(owner, out columns, out rows, out widthPixels, out heightPixels);
            this.ApplyTerminalSessionSizeOnUiThread(owner, columns, rows);

            this.shellStream = this.sshClient.CreateShellStream(
                SshNetShellStreamHelper.DefaultTerminalType,
                columns,
                rows,
                widthPixels,
                heightPixels,
                1024);

            if (!SshNetShellStreamHelper.IsChannelOpen(this.shellStream))
            {
                this.LastError =
                    "SSH shell was closed immediately by the server. Verify login, password, and that the account allows an interactive shell.";
                Logging.Info(this.LastError);
                this.CleanupSessionForReconnect();
                return false;
            }

            string initialText;
            bool immediateEof;
            if (!SshNetShellStreamHelper.TryWaitForShellOutput(this.shellStream, this.streamEncoding, out initialText, out immediateEof))
            {
                this.LastError = immediateEof
                    ? "SSH shell closed before any output was received. Verify password (OpenSSH uses password auth on this host) and that the account has a normal login shell."
                    : "SSH shell did not send a prompt in time. Check host configuration.";
                Logging.Info(this.LastError);
                this.CleanupSessionForReconnect();
                return false;
            }

            this.readLoopHadInitialOutput = !string.IsNullOrEmpty(initialText);
            this.pendingInitialShellText = initialText;
            this.readLoopPendingStart = true;
            Logging.Info(string.Format(
                "SSH: connected to {0}:{1} (PTY {2}x{3}).",
                this.Favorite.ServerName,
                this.Favorite.Port,
                columns,
                rows));
            return true;
        }

        private void ConnectSshClientOnUiThread(IWin32Window owner)
        {
            Control invokeTarget = owner as Control;
            if (invokeTarget == null)
                invokeTarget = this.IsHandleCreated ? (Control)this : this.terminalControl;

            if (invokeTarget != null && invokeTarget.IsHandleCreated && invokeTarget.InvokeRequired)
                invokeTarget.Invoke(new Action(() => this.sshClient.Connect()));
            else
                this.sshClient.Connect();
        }

        private void ApplyTerminalSessionSizeOnUiThread(IWin32Window owner, uint columns, uint rows)
        {
            if (this.IsDisposed || this.terminalControl.IsDisposed)
                return;

            try
            {
                Control invokeTarget = owner as Control;
                if (invokeTarget == null)
                    invokeTarget = this.IsHandleCreated ? (Control)this : this.terminalControl;
                if (!invokeTarget.IsHandleCreated)
                {
                    this.terminalControl.ApplySessionSize((int)columns, (int)rows, force: true);
                    return;
                }

                SshUiThread.RunOnOwner(
                    invokeTarget,
                    () => this.terminalControl.ApplySessionSize((int)columns, (int)rows, force: true));
            }
            catch (Exception exception)
            {
                Logging.Error("SSH: unable to sync VtNetCore size with PTY.", exception);
            }
        }

        private void GetShellGeometryOnUiThread(
            IWin32Window owner,
            out uint columns,
            out uint rows,
            out uint widthPixels,
            out uint heightPixels)
        {
            columns = SshNetShellStreamHelper.DefaultShellColumns;
            rows = SshNetShellStreamHelper.DefaultShellRows;
            widthPixels = columns * 8;
            heightPixels = rows * 16;

            if (this.IsDisposed || this.terminalControl.IsDisposed)
                return;

            try
            {
                Control invokeTarget = owner as Control;
                if (invokeTarget == null)
                    invokeTarget = this.IsHandleCreated ? (Control)this : this.terminalControl;
                if (!invokeTarget.IsHandleCreated)
                    return;

                Tuple<uint, uint, uint, uint> geometry = SshUiThread.RunOnOwner(
                    invokeTarget,
                    () =>
                    {
                        this.terminalControl.GetTerminalDimensions(out int measuredColumns, out int measuredRows);
                        this.terminalControl.GetCellPixelSize(out int charWidth, out int charHeight);
                        charWidth = Math.Max(1, charWidth);
                        charHeight = Math.Max(1, charHeight);
                        uint cols = ClampTerminalDimension((uint)measuredColumns, 20, MaxTerminalColumns);
                        uint rowCount = ClampTerminalDimension((uint)measuredRows, 8, MaxTerminalRows);
                        return Tuple.Create(
                            cols,
                            rowCount,
                            cols * (uint)charWidth,
                            rowCount * (uint)charHeight);
                    });

                columns = geometry.Item1;
                rows = geometry.Item2;
                widthPixels = Math.Max(geometry.Item3, columns * 8);
                heightPixels = Math.Max(geometry.Item4, rows * 16);
            }
            catch (Exception exception)
            {
                Logging.Error("SSH: unable to read terminal size from UI; using default 80x24.", exception);
            }
        }

        private static uint ClampTerminalDimension(uint value, uint minimum, uint maximum)
        {
            if (value < minimum)
                return minimum;
            if (value > maximum)
                return maximum;
            return value;
        }

        public void SyncTerminalAfterLayout()
        {
            if (this.IsDisposed || !this.Connected)
                return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(this.SyncTerminalAfterLayout));
                return;
            }

            this.ResizeTerminalToMatchUi("layout");
        }

        private void SchedulePostConnectTerminalSync()
        {
            this.BeginInvoke(new Action(() => this.ResizeTerminalToMatchUi("post-connect")));
            var layoutTimer = new System.Windows.Forms.Timer { Interval = 250 };
            layoutTimer.Tick += (sender, args) =>
            {
                layoutTimer.Stop();
                layoutTimer.Dispose();
                if (!this.IsDisposed && this.Connected)
                    this.ResizeTerminalToMatchUi("post-connect-delayed");
            };
            layoutTimer.Start();
        }

        private void ResizeTerminalToMatchUi(string reason)
        {
            if (this.IsDisposed || this.shellStream == null || this.sshClient == null || !this.sshClient.IsConnected)
                return;

            int columns;
            int rows;
            this.terminalControl.GetTerminalDimensions(out columns, out rows);
            uint cols = ClampTerminalDimension((uint)columns, 20, MaxTerminalColumns);
            uint rowCount = ClampTerminalDimension((uint)rows, 8, MaxTerminalRows);
            if ((int)cols == this.lastPtyColumns && (int)rowCount == this.lastPtyRows)
                return;

            this.lastPtyColumns = (int)cols;
            this.lastPtyRows = (int)rowCount;
            this.terminalControl.ApplySessionSize((int)cols, (int)rowCount);
            if (SshNetSessionConfigurator.TryResizePty(this.shellStream, cols, rowCount))
            {
                Logging.Info(string.Format(
                    "SSH: terminal resized ({0}) to PTY {1}x{2}.",
                    reason,
                    cols,
                    rowCount));
            }

            this.terminalControl.FlushPendingOutput();
            this.terminalControl.Invalidate();
        }

        private IWin32Window GetDialogOwner()
        {
            if (this.IsDisposed)
                return null;

            if (this.InvokeRequired)
                return (IWin32Window)this.Invoke(new Func<IWin32Window>(this.GetDialogOwner));

            return this.FindForm();
        }

        private void OnTerminalResized(object sender, EventArgs e)
        {
            if (this.shellStream == null || this.sshClient == null || !this.sshClient.IsConnected)
                return;

            this.ResizeTerminalToMatchUi("window-resize");
        }

        private void StartReadLoop(int session, bool hadInitialOutput)
        {
            this.readCancellation = new CancellationTokenSource();
            CancellationToken token = this.readCancellation.Token;
            this.readTask = Task.Factory.StartNew(
                () => this.ReadLoop(session, token, hadInitialOutput),
                token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void ReadLoop(int session, CancellationToken cancellationToken, bool hadInitialOutput)
        {
            var buffer = new byte[4096];
            bool receivedOutput = hadInitialOutput;
            try
            {
                while (!cancellationToken.IsCancellationRequested
                    && session == this.sessionGeneration
                    && this.shellStream != null
                    && this.sshClient != null
                    && this.sshClient.IsConnected)
                {
                    int read = SshNetShellStreamHelper.BlockingRead(
                        this.shellStream,
                        buffer,
                        0,
                        buffer.Length,
                        cancellationToken);
                    if (read <= 0)
                    {
                        if (!receivedOutput)
                        {
                            this.LastError =
                                "SSH shell closed before any output was received. Check username, password, and shell access for this account.";
                            Logging.Info("SSH: shell stream ended with no prior output.");
                            this.SafeAppend("\r\n" + this.LastError + "\r\n");
                        }
                        else if (!SshNetShellStreamHelper.IsChannelOpen(this.shellStream))
                        {
                            Logging.Info("SSH: shell channel closed by server.");
                            this.SafeAppend("\r\n[SSH session ended]\r\n");
                        }

                        break;
                    }

                    receivedOutput = true;
                    string text = this.streamEncoding.GetString(buffer, 0, read);
                    this.SafeAppend(text);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested && session == this.sessionGeneration)
                {
                    Logging.Error("SSH.NET stream read failed.", exception);
                    this.SafeAppend(string.Format("\r\n[SSH.NET read error] {0}\r\n", exception.Message));
                }
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested && session == this.sessionGeneration)
                    this.EndShellSession(receivedOutput);
            }
        }

        private void EndShellSession(bool hadUserVisibleOutput)
        {
            this.CleanupSessionForReconnect();

            if (hadUserVisibleOutput)
            {
                this.SafeAppend("\r\n");
                this.BeginInvoke(new Action(() =>
                {
                    if (!this.IsDisposed)
                        this.terminalControl.FlushPendingOutput();
                }));
                return;
            }

            this.SafeFireDisconnected();
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
            if (this.IsDisposed || string.IsNullOrEmpty(text))
                return;

            lock (this.uiOutputLock)
            {
                this.uiOutputBatch.Append(text);
            }

            if (this.InvokeRequired)
            {
                if (Interlocked.CompareExchange(ref this.uiOutputFlushScheduled, 1, 0) != 0)
                    return;

                this.BeginInvoke(new Action(this.FlushUiOutputBatch));
                return;
            }

            this.FlushUiOutputBatch();
        }

        private void FlushUiOutputBatch()
        {
            if (this.IsDisposed)
                return;

            string chunk;
            lock (this.uiOutputLock)
            {
                if (this.uiOutputBatch.Length == 0)
                {
                    Interlocked.Exchange(ref this.uiOutputFlushScheduled, 0);
                    return;
                }

                chunk = this.uiOutputBatch.ToString();
                this.uiOutputBatch.Length = 0;
            }

            Interlocked.Exchange(ref this.uiOutputFlushScheduled, 0);
            this.terminalControl.AppendAnsi(chunk);

            lock (this.uiOutputLock)
            {
                if (this.uiOutputBatch.Length > 0
                    && Interlocked.CompareExchange(ref this.uiOutputFlushScheduled, 1, 0) == 0)
                    this.BeginInvoke(new Action(this.FlushUiOutputBatch));
            }
        }

        private void SafeFireDisconnected()
        {
            if (this.IsDisposed)
                return;

            if (this.InvokeRequired)
                this.BeginInvoke(new Action(this.FireDisconnected));
            else
                this.FireDisconnected();
        }

        private void CleanupSessionForReconnect()
        {
            this.lastPtyColumns = -1;
            this.lastPtyRows = -1;
            this.readLoopPendingStart = false;
            this.readLoopHadInitialOutput = false;
            this.pendingInitialShellText = null;
            Interlocked.Exchange(ref this.uiOutputFlushScheduled, 0);
            lock (this.uiOutputLock)
            {
                this.uiOutputBatch.Length = 0;
            }

            Interlocked.Increment(ref this.sessionGeneration);

            if (this.readCancellation != null)
            {
                this.readCancellation.Cancel();
            }

            if (this.readTask != null)
            {
                try
                {
                    this.readTask.Wait(ReadTaskJoinTimeoutMs);
                }
                catch (AggregateException)
                {
                }
            }

            if (this.readCancellation != null)
            {
                this.readCancellation.Dispose();
                this.readCancellation = null;
            }

            this.readTask = null;

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref this.connectGeneration);
                this.CleanupSessionForReconnect();

                if (this.terminalControl != null && !this.terminalControl.IsDisposed)
                    this.terminalControl.FlushPendingOutput();
            }

            base.Dispose(disposing);
        }

        bool IConnectionExtra.FullScreen
        {
            get { return false; }
            set { }
        }

        string IConnectionExtra.Server
        {
            get { return this.Favorite != null ? this.Favorite.ServerName : string.Empty; }
        }

        string IConnectionExtra.UserName
        {
            get
            {
                if (this.Favorite == null || this.CredentialFactory == null)
                    return string.Empty;

                var credentials = this.CredentialFactory.CreateCredential(this.Favorite.Security);
                return credentials != null ? credentials.UserName : string.Empty;
            }
        }

        string IConnectionExtra.Domain
        {
            get
            {
                if (this.Favorite == null || this.CredentialFactory == null)
                    return string.Empty;

                var credentials = this.CredentialFactory.CreateCredential(this.Favorite.Security);
                return credentials != null ? credentials.Domain : string.Empty;
            }
        }

        bool IConnectionExtra.ConnectToConsole
        {
            get { return false; }
        }

        bool IFocusable.ContainsFocus
        {
            get { return this.terminalControl.ContainsFocus; }
        }

        void IFocusable.Focus()
        {
            this.terminalControl.FocusTerminal();
        }

    }
}
