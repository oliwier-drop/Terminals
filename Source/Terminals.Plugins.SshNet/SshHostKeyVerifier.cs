// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Terminals.Plugins.SshNet
{
    internal sealed class SshHostKeyVerifier
    {
        private readonly string host;
        private readonly int port;
        private readonly SshKnownHostsStore store;
        private readonly IWin32Window owner;

        internal SshHostKeyVerifier(string host, int port, SshKnownHostsStore store, IWin32Window owner)
        {
            this.host = host;
            this.port = port;
            this.store = store;
            this.owner = owner;
        }

        internal void Attach(SshClient client)
        {
            client.HostKeyReceived += this.OnHostKeyReceived;
        }

        private void OnHostKeyReceived(object sender, HostKeyEventArgs e)
        {
            string fingerprintDisplay = SshHostKeyFingerprint.FormatSha256(e.FingerPrint);
            SshKnownHostEntry known = this.store.Find(this.host, this.port, e.HostKeyName);
            bool keyChanged = known != null && !known.Matches(this.host, this.port, e.HostKeyName, e.FingerPrint);

            if (known != null && known.Matches(this.host, this.port, e.HostKeyName, e.FingerPrint))
            {
                e.CanTrust = true;
                return;
            }

            SshHostKeyTrustChoice choice = SshUiThread.RunOnOwner(
                this.owner,
                () => SshHostKeyTrustDialog.Show(
                    this.owner,
                    this.host,
                    this.port,
                    e.HostKeyName,
                    fingerprintDisplay,
                    keyChanged));

            if (choice == SshHostKeyTrustChoice.Reject)
            {
                e.CanTrust = false;
                return;
            }

            if (choice == SshHostKeyTrustChoice.TrustAlways)
                this.store.AddOrUpdate(this.host, this.port, e.HostKeyName, e.FingerPrint);

            e.CanTrust = true;
        }
    }
}
