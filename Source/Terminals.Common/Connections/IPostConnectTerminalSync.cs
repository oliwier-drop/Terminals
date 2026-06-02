namespace Terminals.Common.Connections
{
    /// <summary>
    /// Terminal connection that needs a UI-thread layout sync after the tab is shown (PTY resize, flush output).
    /// </summary>
    public interface IPostConnectTerminalSync
    {
        void SyncTerminalAfterLayout();
    }
}
