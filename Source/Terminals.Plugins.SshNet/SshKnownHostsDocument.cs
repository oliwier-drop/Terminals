// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Terminals.Plugins.SshNet
{
    [XmlRoot("KnownHosts")]
    public sealed class SshKnownHostsDocument
    {
        public SshKnownHostsDocument()
        {
            this.Entries = new List<SshKnownHostEntry>();
        }

        [XmlArray("Entries")]
        [XmlArrayItem("Host")]
        public List<SshKnownHostEntry> Entries { get; set; }
    }
}
