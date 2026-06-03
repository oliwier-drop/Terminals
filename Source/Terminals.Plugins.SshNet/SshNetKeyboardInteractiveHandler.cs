// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Terminals.Plugins.SshNet
{
    internal static class SshNetKeyboardInteractiveHandler
    {
        internal static KeyboardInteractiveAuthenticationMethod Create(
            string userName,
            string defaultPassword,
            IWin32Window owner)
        {
            var method = new KeyboardInteractiveAuthenticationMethod(userName);
            method.AuthenticationPrompt += (sender, e) =>
                OnAuthenticationPrompt(e, defaultPassword, owner);
            return method;
        }

        private static void OnAuthenticationPrompt(
            AuthenticationPromptEventArgs e,
            string defaultPassword,
            IWin32Window owner)
        {
            foreach (AuthenticationPrompt prompt in e.Prompts)
            {
                bool secret = !prompt.IsEchoed;
                if (secret && !string.IsNullOrEmpty(defaultPassword))
                    prompt.Response = defaultPassword;
                else
                    prompt.Response = PromptForSecret(owner, prompt.Request, secret);
            }
        }

        private static string PromptForSecret(IWin32Window owner, string request, bool secret)
        {
            return SshUiThread.RunOnOwner(owner, () => PromptForSecretOnUiThread(owner, request, secret));
        }

        private static string PromptForSecretOnUiThread(IWin32Window owner, string request, bool secret)
        {
            string caption = "SSH keyboard-interactive authentication";
            string instructions = string.IsNullOrEmpty(request) ? "Enter response:" : request;

            if (secret)
            {
                using (var form = new Form())
                using (var textBox = new TextBox { UseSystemPasswordChar = true, Width = 280 })
                using (var ok = new Button { Text = "OK", DialogResult = DialogResult.OK })
                using (var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel })
                {
                    form.Text = caption;
                    form.FormBorderStyle = FormBorderStyle.FixedDialog;
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.ClientSize = new System.Drawing.Size(300, 120);
                    form.Controls.Add(new Label { Text = instructions, AutoSize = true, Location = new System.Drawing.Point(12, 12) });
                    textBox.Location = new System.Drawing.Point(12, 40);
                    ok.Location = new System.Drawing.Point(120, 75);
                    cancel.Location = new System.Drawing.Point(200, 75);
                    form.Controls.Add(textBox);
                    form.Controls.Add(ok);
                    form.Controls.Add(cancel);
                    form.AcceptButton = ok;
                    form.CancelButton = cancel;

                    return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : string.Empty;
                }
            }

            using (var form = new Form())
            using (var textBox = new TextBox { Width = 280 })
            using (var ok = new Button { Text = "OK", DialogResult = DialogResult.OK })
            using (var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel })
            {
                form.Text = caption;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new System.Drawing.Size(300, 120);
                form.Controls.Add(new Label { Text = instructions, AutoSize = true, Location = new System.Drawing.Point(12, 12) });
                textBox.Location = new System.Drawing.Point(12, 40);
                ok.Location = new System.Drawing.Point(120, 75);
                cancel.Location = new System.Drawing.Point(200, 75);
                form.Controls.Add(textBox);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : string.Empty;
            }
        }
    }
}
