// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// SSH connection behavior profile (algorithm negotiation and PTY/shell expectations).
    /// </summary>
    public enum SshConnectionProfile : byte
    {
        /// <summary>Linux / OpenSSH servers — modern defaults and xterm-256color PTY.</summary>
        Server = 0,

        /// <summary>Switches and routers (e.g. Extreme EXOS) — constrained algorithms and vt100 PTY.</summary>
        NetworkDevice = 1
    }
}
