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
