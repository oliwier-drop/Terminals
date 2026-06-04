// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
namespace Terminals.Plugins.SshNet
{
    internal sealed class SshSessionCredentials
    {
        internal string UserName { get; set; }

        internal string Password { get; set; }

        internal string Domain { get; set; }
    }
}
