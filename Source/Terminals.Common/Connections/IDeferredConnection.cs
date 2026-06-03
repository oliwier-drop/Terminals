// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using System;

namespace Terminals.Common.Connections
{
    /// <summary>
    /// Connection that completes handshake on a background thread and reports back on the UI thread.
    /// </summary>
    public interface IDeferredConnection
    {
        bool IsConnectInProgress { get; }

        void BeginConnect(Action<bool> completed);
    }
}
