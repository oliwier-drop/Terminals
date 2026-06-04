// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;

namespace Terminals.Plugins.SshNet
{
    [Serializable]
    public sealed class SshKnownHostEntry
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public string HostKeyName { get; set; }

        /// <summary>Base64-encoded host key fingerprint bytes from SSH.NET.</summary>
        public string Fingerprint { get; set; }

        public bool Matches(string host, int port, string hostKeyName, byte[] fingerprint)
        {
            if (fingerprint == null || fingerprint.Length == 0)
                return false;

            return string.Equals(this.Host, host, StringComparison.OrdinalIgnoreCase)
                && this.Port == port
                && string.Equals(this.HostKeyName, hostKeyName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(this.Fingerprint, Convert.ToBase64String(fingerprint), StringComparison.Ordinal);
        }
    }
}
