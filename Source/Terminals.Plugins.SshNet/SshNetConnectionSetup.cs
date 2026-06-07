// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using Renci.SshNet;
namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// Result of mapping favorite SSH options to SSH.NET connection and session features.
    /// </summary>
    internal sealed class SshNetConnectionSetup
    {
        internal SshNetConnectionSetup(
            ConnectionInfo connectionInfo,
            string host,
            int port,
            SshOptions sshOptions,
            bool enableCompression,
            bool x11Forwarding,
            bool enablePagentAuthentication,
            bool enablePagentForwarding,
            bool verbose,
            string sessionName)
        {
            this.ConnectionInfo = connectionInfo;
            this.Host = host;
            this.Port = port;
            this.SshOptions = sshOptions;
            this.EnableCompression = enableCompression;
            this.X11Forwarding = x11Forwarding;
            this.EnablePagentAuthentication = enablePagentAuthentication;
            this.EnablePagentForwarding = enablePagentForwarding;
            this.Verbose = verbose;
            this.SessionName = sessionName;
        }

        public ConnectionInfo ConnectionInfo { get; private set; }

        public string Host { get; private set; }

        public int Port { get; private set; }

        public SshOptions SshOptions { get; private set; }

        public bool EnableCompression { get; private set; }

        public bool X11Forwarding { get; private set; }

        public bool EnablePagentAuthentication { get; private set; }

        public bool EnablePagentForwarding { get; private set; }

        public bool Verbose { get; private set; }

        public string SessionName { get; private set; }
    }
}
