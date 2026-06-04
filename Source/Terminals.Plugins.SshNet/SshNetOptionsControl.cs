// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Windows.Forms;
using Terminals.Forms.EditFavorite;
using Terminals.Plugins.Putty;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// SSH options panel for the SSH.NET plugin (PuTTY-only features hidden).
    /// </summary>
    public class SshNetOptionsControl : SshOptionsControl, IProtocolOptionsControl
    {
        public SshNetOptionsControl()
        {
            this.ConfigureForSshNetPlugin();
        }

        protected override void OnKeysButtonClick()
        {
            MessageBox.Show(
                "Store private keys in Terminals application settings under the SSH keys section, then reference them using KeyTag on the favorite.\r\n\r\nAlternatively set KeyFile to a private key path.",
                "SSH keys",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
