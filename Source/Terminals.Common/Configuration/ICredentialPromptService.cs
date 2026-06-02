using System.Windows.Forms;

namespace Terminals.Configuration
{
    public interface ICredentialPromptService
    {
        ConnectionCredentialPromptResult PromptForSshConnection(
            IWin32Window owner,
            string host,
            string defaultUserName,
            string defaultPassword,
            string defaultDomain);
    }
}
