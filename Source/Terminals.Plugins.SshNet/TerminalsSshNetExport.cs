// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using Terminals.Integration.Export;
using Terminals.Plugins.Putty;

namespace Terminals.Plugins.SshNet
{
    internal class TerminalsSshNetExport : ITerminalsOptionsExport
    {
        public void ExportOptions(IExportOptionsContext context)
        {
            if (context.Favorite.Protocol == SshProtocol.Name)
            {
                context.WriteElementString("sshSessionName", context.Favorite.SshSessionName);
                context.WriteElementString("sshVerbose", context.Favorite.SshVerbose.ToString());
                context.WriteElementString("sshEnablePagentAuthentication", context.Favorite.SshEnablePagentAuthentication.ToString());
                context.WriteElementString("sshEnablePagentForwarding", context.Favorite.SshEnablePagentForwarding.ToString());
                context.WriteElementString("sshX11Forwarding", context.Favorite.SshX11Forwarding.ToString());
                context.WriteElementString("sshEnableCompression", context.Favorite.SshEnableCompression.ToString());
                context.WriteElementString("sshVersion", context.Favorite.SshVersion.ToString());
            }
        }
    }
}
