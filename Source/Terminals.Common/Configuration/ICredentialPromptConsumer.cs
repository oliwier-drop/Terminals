// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
namespace Terminals.Configuration
{
    public interface ICredentialPromptConsumer
    {
        ICredentialPromptService CredentialPromptService { get; set; }
    }
}
