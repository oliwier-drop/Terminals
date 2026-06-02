using System;
using System.Collections.Generic;
using Renci.SshNet;
using Renci.SshNet.Compression;
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

            string userName = credentials.UserName ?? string.Empty;
            string password = credentials.Password;

            var methods = new List<AuthenticationMethod>();
            if (!string.IsNullOrEmpty(password))
                methods.Add(new PasswordAuthenticationMethod(userName, password));
            else
                methods.Add(new NoneAuthenticationMethod(userName));

            var connectionInfo = new ConnectionInfo(host, port, userName, methods.ToArray());
            ApplyCompression(connectionInfo, sshOptions != null && sshOptions.EnableCompression);

            bool x11 = sshOptions != null && sshOptions.X11Forwarding;
            bool pageantAuth = sshOptions != null && sshOptions.EnablePagentAuthentication;
            bool pageantForward = sshOptions != null && sshOptions.EnablePagentForwarding;
            bool verbose = sshOptions != null && sshOptions.Verbose;
            string sessionName = sshOptions != null ? sshOptions.SessionName : null;

            setup = new SshNetConnectionSetup(
                connectionInfo,
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
