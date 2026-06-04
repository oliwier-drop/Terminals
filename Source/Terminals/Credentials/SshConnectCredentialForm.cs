// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Terminals.Credentials
{
    internal partial class SshConnectCredentialForm : Form
    {
        private readonly TextBox userTextBox;
        private readonly TextBox passwordTextBox;

        internal SshConnectCredentialForm(string host, string defaultUserName, string defaultPassword)
        {
            this.Text = "SSH sign in";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ClientSize = new Size(360, 150);

            var targetLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Text = string.IsNullOrEmpty(host) ? "Enter credentials:" : "Connect to " + host
            };

            var userLabel = new Label { AutoSize = true, Location = new Point(12, 36), Text = "User name:" };
            this.userTextBox = new TextBox { Location = new Point(110, 32), Width = 230, Text = defaultUserName ?? string.Empty };

            var passwordLabel = new Label { AutoSize = true, Location = new Point(12, 68), Text = "Password:" };
            this.passwordTextBox = new TextBox
            {
                Location = new Point(110, 64),
                Width = 230,
                UseSystemPasswordChar = true,
                Text = defaultPassword ?? string.Empty
            };

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(170, 110), Size = new Size(80, 28) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(260, 110), Size = new Size(80, 28) };

            this.Controls.Add(targetLabel);
            this.Controls.Add(userLabel);
            this.Controls.Add(this.userTextBox);
            this.Controls.Add(passwordLabel);
            this.Controls.Add(this.passwordTextBox);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);

            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        internal string UserName
        {
            get { return this.userTextBox.Text.Trim(); }
        }

        internal string Password
        {
            get { return this.passwordTextBox.Text; }
        }
    }
}
