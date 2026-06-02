using System;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// Applies SSH session options after the client is connected (step 7 features).
    /// </summary>
    internal static class SshNetSessionConfigurator
    {
        internal static void AttachHostKeyHandler(SshClient client)
        {
            client.HostKeyReceived += (sender, e) =>
            {
                // Step 7: replace with trusted host key store and user prompt.
                e.CanTrust = true;
            };
        }

        internal static void ApplyPostConnectFeatures(SshClient client, SshNetConnectionSetup setup)
        {
            if (setup == null)
                return;

            if (setup.Verbose)
                Logging.Info("SSH.NET verbose logging enabled for session.");

            if (setup.EnablePagentAuthentication)
                Logging.Info("SSH.NET: Pageant authentication is not implemented yet (step 7).");

            if (setup.EnablePagentForwarding)
                Logging.Info("SSH.NET: agent forwarding is not implemented yet (step 7).");

            if (setup.X11Forwarding)
                Logging.Info("SSH.NET: X11 forwarding is not implemented yet (step 7).");
        }

        internal static bool TryResizePty(ShellStream shellStream, uint columns, uint rows)
        {
            return SshNetShellStreamHelper.TrySendWindowChange(shellStream, columns, rows);
        }
    }
}
