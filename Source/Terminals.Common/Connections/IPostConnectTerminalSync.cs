// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
namespace Terminals.Common.Connections
{
    /// <summary>
    /// Terminal connection that needs a UI-thread layout sync after the tab is shown (PTY resize, flush output).
    /// </summary>
    public interface IPostConnectTerminalSync
    {
        void SyncTerminalAfterLayout();
    }
}
