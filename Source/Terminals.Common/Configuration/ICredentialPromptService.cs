// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Windows.Forms;

namespace Terminals.Configuration
{
    public interface ICredentialPromptService
    {
        ConnectionCredentialPromptResult PromptForSshConnection(
            IWin32Window owner,
            string host,
            string defaultUserName,
            string defaultPassword,
            string defaultDomain);
    }
}
