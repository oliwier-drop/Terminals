// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;
using Terminals.Common.Connections;
using Terminals.Plugins.SshNet.Properties;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// SSH protocol identity (port, display name, tree icon) for the SSH.NET plugin.
    /// </summary>
    public static class SshProtocol
    {
        public const int Port = 22;

        public const string Name = KnownConnectionConstants.SSH;

        public static readonly Image TreeIconSsh = Resources.treeIcon_ssh;
    }
}
