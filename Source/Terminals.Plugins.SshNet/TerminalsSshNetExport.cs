using Terminals.Integration.Export;

namespace Terminals.Plugins.SshNet
{
    internal class TerminalsSshNetExport : ITerminalsOptionsExport
    {
        public void ExportOptions(IExportOptionsContext context)
        {
            if (context.Favorite.Protocol == SshNetConnectionPlugin.SshNet)
            {
                context.WriteElementString("sshSessionName", context.Favorite.SshSessionName);
                context.WriteElementString("sshVerbose", context.Favorite.SshVerbose.ToString());
                context.WriteElementString("sshEnablePagentAuthentication", context.Favorite.SshEnablePagentAuthentication.ToString());
                context.WriteElementString("sshEnablePagentForwarding", context.Favorite.SshEnablePagentForwarding.ToString());
                context.WriteElementString("sshX11Forwarding", context.Favorite.SshX11Forwarding.ToString());
                context.WriteElementString("sshEnableCompression", context.Favorite.SshEnableCompression.ToString());
                context.WriteElementString("sshVersion", context.Favorite.SshVersion.ToString());
            }
        }
    }
}
