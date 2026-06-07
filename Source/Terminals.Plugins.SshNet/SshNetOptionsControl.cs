// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Windows.Forms;
using Terminals.Data;
using Terminals.Forms.EditFavorite;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// SSH options panel for the SSH.NET plugin.
    /// </summary>
    public partial class SshNetOptionsControl : UserControl, IProtocolOptionsControl
    {
        public SshNetOptionsControl()
        {
            this.InitializeComponent();
            this.comboBoxProfile.SelectedIndex = 0;
            this.UpdateCompressionAvailability();
        }

        public void LoadFrom(IFavorite favorite)
        {
            this.LoadFrom(favorite.ProtocolProperties);
        }

        private void LoadFrom(ProtocolOptions protocolOptions)
        {
            var sshOptions = protocolOptions as SshOptions;
            if (sshOptions == null)
                return;

            this.comboBoxProfile.SelectedIndex = ProfileToIndex(sshOptions.ConnectionProfile);
            this.checkBoxCompression.Checked = sshOptions.EnableCompression;
            this.UpdateCompressionAvailability();
        }

        public void SaveTo(IFavorite favorite)
        {
            this.SaveTo(favorite.ProtocolProperties);
        }

        private void SaveTo(ProtocolOptions protocolOptions)
        {
            var sshOptions = protocolOptions as SshOptions;
            if (sshOptions == null)
                return;

            sshOptions.ConnectionProfile = IndexToProfile(this.comboBoxProfile.SelectedIndex);
            sshOptions.EnableCompression = this.checkBoxCompression.Checked;
        }

        private void ComboBoxProfile_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            this.UpdateCompressionAvailability();
        }

        private void UpdateCompressionAvailability()
        {
            bool networkDevice = this.comboBoxProfile.SelectedIndex == ProfileToIndex(SshConnectionProfile.NetworkDevice);
            this.checkBoxCompression.Enabled = !networkDevice;
            if (networkDevice)
                this.checkBoxCompression.Checked = false;
        }

        private static int ProfileToIndex(SshConnectionProfile profile)
        {
            return profile == SshConnectionProfile.NetworkDevice ? 1 : 0;
        }

        private static SshConnectionProfile IndexToProfile(int index)
        {
            return index == 1 ? SshConnectionProfile.NetworkDevice : SshConnectionProfile.Server;
        }

        private void KeysButton_Click(object sender, System.EventArgs e)
        {
            MessageBox.Show(
                "Store private keys in Terminals application settings under the SSH keys section, then reference them using KeyTag on the favorite.\r\n\r\nAlternatively set KeyFile to a private key path.",
                "SSH keys",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
