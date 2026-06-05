// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using Terminals.Common.Configuration;
using Terminals.Data;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// SSH connection options for the SSH.NET plugin (config round-trip includes legacy PuTTY fields).
    /// </summary>
    [Serializable]
    public class SshOptions : ProtocolOptions
    {
        public string SessionName { get; set; }

        public bool Verbose { get; set; }

        /// <summary>Legacy PuTTY: Pageant authentication (logged as unsupported by SSH.NET).</summary>
        public bool EnablePagentAuthentication { get; set; }

        /// <summary>Legacy PuTTY: agent forwarding (logged as unsupported by SSH.NET).</summary>
        public bool EnablePagentForwarding { get; set; }

        /// <summary>Legacy PuTTY: X11 forwarding (logged as unsupported by SSH.NET).</summary>
        public bool X11Forwarding { get; set; }

        public bool EnableCompression { get; set; }

        public SshVersion SshVersion { get; set; }

        public SshConnectionProfile ConnectionProfile { get; set; }

        public AuthMethod AuthMethod { get; set; }

        /// <summary>Name of a key in application SSH key store.</summary>
        public string KeyTag { get; set; }

        /// <summary>Path to a private key file (OpenSSH PEM or PuTTY .ppk when supported).</summary>
        public string KeyFile { get; set; }

        public override ProtocolOptions Copy()
        {
            return new SshOptions
            {
                SessionName = this.SessionName,
                Verbose = this.Verbose,
                EnablePagentAuthentication = this.EnablePagentAuthentication,
                EnablePagentForwarding = this.EnablePagentForwarding,
                X11Forwarding = this.X11Forwarding,
                EnableCompression = this.EnableCompression,
                SshVersion = this.SshVersion,
                ConnectionProfile = this.ConnectionProfile,
                AuthMethod = this.AuthMethod,
                KeyTag = this.KeyTag,
                KeyFile = this.KeyFile
            };
        }
    }
}
