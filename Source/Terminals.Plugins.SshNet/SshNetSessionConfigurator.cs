// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using Renci.SshNet;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// Applies SSH session options after the client is connected.
    /// </summary>
    internal static class SshNetSessionConfigurator
    {
        internal static void ApplyPostConnectFeatures(SshClient client, SshNetConnectionSetup setup)
        {
            if (setup == null)
                return;

            if (setup.Verbose)
                Logging.Info("SSH.NET verbose logging enabled for session.");

            if (setup.EnablePagentAuthentication)
                Logging.Info("SSH.NET: Pageant authentication is not supported. Use KeyTag, KeyFile, or password authentication.");

            if (setup.EnablePagentForwarding)
                Logging.Info("SSH.NET: SSH agent forwarding is not implemented for this plugin.");

            if (setup.X11Forwarding)
                Logging.Info("SSH.NET: X11 forwarding is not implemented for this plugin.");

            if (!string.IsNullOrEmpty(setup.SessionName))
                Logging.Info("SSH.NET: PuTTY session name '" + setup.SessionName + "' is not used by the SSH.NET plugin.");
        }

        internal static bool TryResizePty(ShellStream shellStream, uint columns, uint rows)
        {
            return TryResizePty(shellStream, columns, rows, 0u, 0u);
        }

        internal static bool TryResizePty(
            ShellStream shellStream,
            uint columns,
            uint rows,
            uint widthPixels,
            uint heightPixels)
        {
            return SshNetShellStreamHelper.TrySendWindowChange(
                shellStream,
                columns,
                rows,
                widthPixels,
                heightPixels);
        }
    }
}
