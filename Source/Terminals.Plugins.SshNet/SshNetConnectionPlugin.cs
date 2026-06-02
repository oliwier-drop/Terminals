using System;
using System.Drawing;
using System.Windows.Forms;
using Terminals.Common.Configuration;
using Terminals.Common.Connections;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Integration.Export;
using Terminals.Plugins.Putty;

namespace Terminals.Plugins.SshNet
{
    internal class SshNetConnectionPlugin : IConnectionPlugin, IOptionsConverterFactory, IOptionsExporterFactory
    {
        internal const int SshPort = 22;
        internal const string SshNet = "SSH.NET";

        public int Port { get { return SshPort; } }

        public string PortName { get { return SshNet; } }

        public Connection CreateConnection()
        {
            return new SshNetConnection();
        }

        public ProtocolOptions CreateOptions()
        {
            return new SshOptions { AuthMethod = AuthMethod.Password };
        }

        public Control[] CreateOptionsControls()
        {
            return new Control[] { new SshNetOptionsControl { Name = "SSH.NET" } };
        }

        public IOptionsConverter CreatOptionsConverter()
        {
            return new SshNetOptionsConverter();
        }

        public Image GetIcon()
        {
            return Connection.Terminalsicon;
        }

        public Type GetOptionsType()
        {
            return typeof(SshOptions);
        }

        public ITerminalsOptionsExport CreateOptionsExporter()
        {
            return new TerminalsSshNetExport();
        }
    }
}
