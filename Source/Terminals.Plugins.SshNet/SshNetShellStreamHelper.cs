using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Terminals.Plugins.SshNet
{
    internal static class SshNetShellStreamHelper
    {
        internal const string DefaultTerminalType = "xterm";
        internal const uint DefaultShellColumns = 80;
        internal const uint DefaultShellRows = 24;
        internal static readonly TimeSpan InitialShellWaitTimeout = TimeSpan.FromSeconds(5);

        private static readonly FieldInfo ChannelField = typeof(ShellStream).GetField("_channel", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static IDictionary<TerminalModes, uint> CreateCompatTerminalModes()
        {
            return new Dictionary<TerminalModes, uint>
            {
                { TerminalModes.VINTR, 3 },
                { TerminalModes.VQUIT, 28 },
                { TerminalModes.VSUSP, 26 },
                { TerminalModes.VEOF, 4 },
                { TerminalModes.VEOL, 0 },
                { TerminalModes.VERASE, 127 },
                { TerminalModes.VWERASE, 23 },
                { TerminalModes.VKILL, 21 },
                { TerminalModes.VREPRINT, 18 },
                { TerminalModes.ICRNL, 1 },
                { TerminalModes.ONLCR, 1 },
                { TerminalModes.ECHO, 1 },
                { TerminalModes.ISIG, 1 },
                { TerminalModes.ICANON, 1 },
                { TerminalModes.TTY_OP_ISPEED, 38400 },
                { TerminalModes.TTY_OP_OSPEED, 38400 }
            };
        }

        /// <summary>
        /// Blocks until the shell sends at least one byte (without ShellStream.Expect, which can drain the channel).
        /// </summary>
        internal static bool TryWaitForShellOutput(
            ShellStream shellStream,
            Encoding encoding,
            out string initialText,
            out bool immediateEof)
        {
            initialText = null;
            immediateEof = false;

            if (shellStream == null)
                return false;

            if (encoding == null)
                encoding = Encoding.UTF8;

            if (!IsChannelOpen(shellStream))
            {
                immediateEof = true;
                return false;
            }

            var builder = new StringBuilder();
            var buffer = new byte[4096];
            int elapsed = 0;
            int waitMs = (int)InitialShellWaitTimeout.TotalMilliseconds;

            while (elapsed < waitMs)
            {
                if (!IsChannelOpen(shellStream))
                {
                    immediateEof = true;
                    return false;
                }

                if (WaitForIncomingData(shellStream, 50, CancellationToken.None))
                {
                    int read = shellStream.Read(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        builder.Append(encoding.GetString(buffer, 0, read));
                        initialText = builder.ToString();
                        Logging.Info("SSH: shell initial output received (" + initialText.Length + " chars).");
                        return true;
                    }

                    if (read == 0 && !IsChannelOpen(shellStream))
                    {
                        immediateEof = true;
                        return false;
                    }
                }

                elapsed += 50;
            }

            Logging.Info("SSH: timed out waiting for shell output (" + InitialShellWaitTimeout.TotalSeconds + "s).");
            immediateEof = !IsChannelOpen(shellStream);
            return false;
        }

        /// <summary>
        /// SSH.NET 2020 ShellStream.Read is non-blocking; an empty queue returns 0. Wait for DataReceived before reading.
        /// </summary>
        internal static int BlockingRead(
            ShellStream shellStream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            if (shellStream == null || buffer == null || count <= 0)
                return 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                int read = shellStream.Read(buffer, offset, count);
                if (read > 0)
                    return read;

                if (!IsChannelOpen(shellStream))
                    return 0;

                if (!WaitForIncomingData(shellStream, 500, cancellationToken))
                    continue;
            }

            return 0;
        }

        internal static bool WaitForIncomingData(
            ShellStream shellStream,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            if (shellStream == null)
                return false;

            if (shellStream.DataAvailable)
                return true;

            if (!IsChannelOpen(shellStream))
                return false;

            using (var dataSignal = new AutoResetEvent(false))
            {
                EventHandler<ShellDataEventArgs> handler = (sender, args) => dataSignal.Set();
                shellStream.DataReceived += handler;
                try
                {
                    int elapsed = 0;
                    const int sliceMs = 50;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (shellStream.DataAvailable)
                            return true;

                        if (!IsChannelOpen(shellStream))
                            return false;

                        if (timeoutMs >= 0 && elapsed >= timeoutMs)
                            return shellStream.DataAvailable;

                        int waitMs = timeoutMs >= 0
                            ? Math.Min(sliceMs, timeoutMs - elapsed)
                            : sliceMs;

                        if (cancellationToken.WaitHandle.WaitOne(waitMs))
                            return shellStream.DataAvailable;

                        dataSignal.WaitOne(waitMs);
                        elapsed += waitMs;
                    }
                }
                finally
                {
                    shellStream.DataReceived -= handler;
                }
            }

            return shellStream.DataAvailable;
        }

        internal static bool IsChannelOpen(ShellStream shellStream)
        {
            if (shellStream == null || ChannelField == null)
                return false;

            try
            {
                object channel = ChannelField.GetValue(shellStream);
                if (channel == null)
                    return false;

                var isOpenProperty = channel.GetType().GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.Public);
                if (isOpenProperty != null)
                    return (bool)isOpenProperty.GetValue(channel, null);

                var isClosedProperty = channel.GetType().GetProperty("IsClosed", BindingFlags.Instance | BindingFlags.Public);
                if (isClosedProperty != null)
                    return !(bool)isClosedProperty.GetValue(channel, null);

                return true;
            }
            catch
            {
                return false;
            }
        }

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
