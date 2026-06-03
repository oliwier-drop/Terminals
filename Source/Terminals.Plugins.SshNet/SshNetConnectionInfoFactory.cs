// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Compression;
using Terminals.Common.Configuration;
using Terminals.Data;
using Terminals.Plugins.Putty;

namespace Terminals.Plugins.SshNet
{
    internal static class SshNetConnectionInfoFactory
    {
        private static readonly Type ZlibType = typeof(ConnectionInfo).Assembly.GetType("Renci.SshNet.Compression.Zlib", false);

        internal const string SshVersion1NotSupported = "SSH.NET supports SSH protocol 2 only. Change the SSH version in connection options.";

        internal static bool TryCreate(
            string host,
            int port,
            IGuardedSecurity credentials,
            SshOptions sshOptions,
            KeysSection sshKeys,
            IWin32Window owner,
            out SshNetConnectionSetup setup,
            out string error)
        {
            var session = new SshSessionCredentials
            {
                UserName = credentials != null ? credentials.UserName : string.Empty,
                Password = credentials != null ? credentials.Password : null
            };
            return TryCreate(host, port, session, sshOptions, sshKeys, owner, out setup, out error);
        }

        internal static bool TryCreate(
            string host,
            int port,
            SshSessionCredentials credentials,
            SshOptions sshOptions,
            KeysSection sshKeys,
            IWin32Window owner,
            out SshNetConnectionSetup setup,
            out string error)
        {
            setup = null;
            error = null;

            if (sshOptions != null && sshOptions.SshVersion == SshVersion.SshVersion1)
            {
                error = SshVersion1NotSupported;
                return false;
            }

            string userName = credentials != null ? credentials.UserName : string.Empty;
            string password = credentials != null ? credentials.Password : null;

            AuthenticationMethod[] methods;
            if (!SshNetAuthenticationBuilder.TryBuildMethods(userName, password, sshOptions, sshKeys, owner, out methods, out error))
                return false;

            var connectionInfo = new ConnectionInfo(host, port, userName, methods);
            ApplyCompression(connectionInfo, sshOptions != null && sshOptions.EnableCompression);

            bool x11 = sshOptions != null && sshOptions.X11Forwarding;
            bool pageantAuth = sshOptions != null && sshOptions.EnablePagentAuthentication;
            bool pageantForward = sshOptions != null && sshOptions.EnablePagentForwarding;
            bool verbose = sshOptions != null && sshOptions.Verbose;
            string sessionName = sshOptions != null ? sshOptions.SessionName : null;

            setup = new SshNetConnectionSetup(
                connectionInfo,
                host,
                port,
                sshOptions,
                sshOptions != null && sshOptions.EnableCompression,
                x11,
                pageantAuth,
                pageantForward,
                verbose,
                sessionName);

            return true;
        }

        private static void ApplyCompression(ConnectionInfo connectionInfo, bool enableCompression)
        {
            if (!enableCompression)
                return;

            connectionInfo.CompressionAlgorithms.Clear();
            connectionInfo.CompressionAlgorithms.Add("zlib@openssh.com", typeof(ZlibOpenSsh));
            if (ZlibType != null)
                connectionInfo.CompressionAlgorithms.Add("zlib", ZlibType);
            connectionInfo.CompressionAlgorithms.Add("none", null);
        }
    }
}
