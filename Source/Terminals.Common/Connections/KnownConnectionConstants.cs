using System;

namespace Terminals.Common.Connections
{
    public class KnownConnectionConstants
    {
        public const int RDPPort = 3389;

        public const int HTTPPort = 80;

        public const string HTTP = "HTTP";

        public const string HTTPS = "HTTPS";

        public const string RAS = "RAS";

        public const string RDP = "RDP";

        public const string SSH = "SSH";

        private const string LegacySshNetProtocol = "SSH.NET";

        public static string NormalizeProtocolName(string protocolName)
        {
            if (string.Equals(protocolName, LegacySshNetProtocol, StringComparison.Ordinal))
                return SSH;

            return protocolName;
        }
    }
}
