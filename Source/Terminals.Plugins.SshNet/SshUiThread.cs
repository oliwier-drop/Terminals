using System;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// Marshals WinForms UI work onto the control's thread (SSH.NET callbacks run on worker threads).
    /// </summary>
    internal static class SshUiThread
    {
        internal static T RunOnOwner<T>(IWin32Window owner, Func<T> action)
        {
            var control = owner as Control;
            if (control != null && control.InvokeRequired)
                return (T)control.Invoke(action);

            return action();
        }

        internal static void RunOnOwner(IWin32Window owner, Action action)
        {
            RunOnOwner(owner, () =>
            {
                action();
                return (object)null;
            });
        }
    }
}
