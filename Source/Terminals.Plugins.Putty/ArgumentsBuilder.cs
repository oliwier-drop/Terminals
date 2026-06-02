using System;
using System.Text;
using Terminals.Data;

namespace Terminals.Plugins.Putty
{
    internal class ArgumentsBuilder
    {
        private readonly IGuardedSecurity credentials;
        private readonly IFavorite favorite;
        private PuttyOptions puttyOptions;

        public ArgumentsBuilder(IGuardedSecurity credentials, IFavorite favorite)
        {
            this.credentials = credentials;
            this.favorite = favorite;
        }

        private void ValidateGeneral()
        {
            if (favorite.Protocol != TelnetConnectionPlugin.TELNET)
                throw new ArgumentException(string.Format("Protocol {0} is not supported", favorite.Protocol));
        }

        public string Build()
        {
            ValidateGeneral();

            puttyOptions = favorite.ProtocolProperties as PuttyOptions;
            return BuildTelnet();
        }

        internal string BuildTelnet()
        {
            var args = new StringBuilder();
            var telnetOptions = puttyOptions as TelnetOptions;

            // 3.8.3.1 -load: load a saved session
            if (!string.IsNullOrEmpty(telnetOptions.SessionName))
                args.AppendFormat(" -load \"{0}\"", telnetOptions.SessionName);

            // 3.8.3.2 Selecting a protocol: -ssh, -telnet, -rlogin, -raw -serial
            args.Append(" -telnet");

            // 3.8.3.3 -v: increase verbosity
            if (telnetOptions.Verbose)
                args.Append(" -v");

            // 3.8.3.7 -P: specify a port number
            if (favorite.Port > 0)
                args.AppendFormat(" -P {0}", favorite.Port);

            args.Append(" " + favorite.ServerName);

            return args.ToString();
        }
    }
}
