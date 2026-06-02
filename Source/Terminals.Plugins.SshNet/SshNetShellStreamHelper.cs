using System;
using System.Reflection;
using Renci.SshNet;

namespace Terminals.Plugins.SshNet
{
    internal static class SshNetShellStreamHelper
    {
        private static readonly FieldInfo ChannelField = typeof(ShellStream).GetField("_channel", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool TrySendWindowChange(ShellStream shellStream, uint columns, uint rows)
        {
            if (shellStream == null || ChannelField == null)
                return false;

            try
            {
                object channel = ChannelField.GetValue(shellStream);
                if (channel == null)
                    return false;

                MethodInfo resizeMethod = channel.GetType().GetMethod("SendWindowChangeRequest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (resizeMethod == null)
                    return false;

                resizeMethod.Invoke(channel, new object[] { columns, rows, 0u, 0u });
                return true;
            }
            catch (Exception exception)
            {
                Logging.Error("SSH.NET PTY resize failed.", exception);
                return false;
            }
        }
    }
}
