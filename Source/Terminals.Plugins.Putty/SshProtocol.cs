using System.Drawing;
using Terminals.Common.Connections;
using Terminals.Plugins.Putty.Properties;

namespace Terminals.Plugins.Putty
{
    /// <summary>
    /// Shared SSH protocol identity (port, display name, tree icon) for the SSH.NET plugin.
    /// </summary>
    public static class SshProtocol
    {
        public const int Port = 22;

        public const string Name = KnownConnectionConstants.SSH;

        public static readonly Image TreeIconSsh = Resources.treeIcon_ssh;
    }
}
