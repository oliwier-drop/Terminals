using System;

namespace Terminals.Common.Connections
{
    /// <summary>
    /// Connection that completes handshake on a background thread and reports back on the UI thread.
    /// </summary>
    public interface IDeferredConnection
    {
        bool IsConnectInProgress { get; }

        void BeginConnect(Action<bool> completed);
    }
}
