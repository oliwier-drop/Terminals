// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
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
        public int Port { get { return SshProtocol.Port; } }

        public string PortName { get { return SshProtocol.Name; } }

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
            return new Control[] { new SshNetOptionsControl { Name = SshProtocol.Name } };
        }

        public IOptionsConverter CreatOptionsConverter()
        {
            return new SshNetOptionsConverter();
        }

        public Image GetIcon()
        {
            return SshProtocol.TreeIconSsh;
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
