// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet
{
    internal enum SshHostKeyTrustChoice
    {
        Reject,
        TrustOnce,
        TrustAlways
    }

    internal sealed class SshHostKeyTrustDialog : Form
    {
        private readonly Label messageLabel;

        internal SshHostKeyTrustDialog(string title, string message, bool keyChanged)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(520, 200);

            this.messageLabel = new Label
            {
                AutoSize = false,
                Location = new Point(12, 12),
                Size = new Size(496, 120),
                Text = message
            };

            var trustOnce = new Button
            {
                Text = keyChanged ? "Continue once" : "Trust once",
                DialogResult = DialogResult.Yes,
                Location = new Point(140, 150),
                Size = new Size(110, 28)
            };
            var trustAlways = new Button
            {
                Text = "Trust always",
                DialogResult = DialogResult.OK,
                Location = new Point(260, 150),
                Size = new Size(110, 28)
            };
            var reject = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(380, 150),
                Size = new Size(110, 28)
            };

            this.Controls.Add(this.messageLabel);
            this.Controls.Add(trustOnce);
            this.Controls.Add(trustAlways);
            this.Controls.Add(reject);
            this.AcceptButton = trustAlways;
            this.CancelButton = reject;
        }

        internal static SshHostKeyTrustChoice Show(IWin32Window owner, string host, int port, string hostKeyName, string fingerprint, bool keyChanged)
        {
            string title = keyChanged ? "SSH host key changed" : "Unknown SSH host key";
            string message = keyChanged
                ? string.Format(
                    "The host key for {0}:{1} has changed.\r\n\r\nAlgorithm: {2}\r\nFingerprint: {3}\r\n\r\nThis may indicate a man-in-the-middle attack. Continue only if you expect this change.",
                    host, port, hostKeyName, fingerprint)
                : string.Format(
                    "The host key for {0}:{1} is not trusted.\r\n\r\nAlgorithm: {2}\r\nFingerprint: {3}",
                    host, port, hostKeyName, fingerprint);

            using (var dialog = new SshHostKeyTrustDialog(title, message, keyChanged))
            {
                DialogResult result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                switch (result)
                {
                    case DialogResult.OK:
                        return SshHostKeyTrustChoice.TrustAlways;
                    case DialogResult.Yes:
                        return SshHostKeyTrustChoice.TrustOnce;
                    default:
                        return SshHostKeyTrustChoice.Reject;
                }
            }
        }
    }
}
