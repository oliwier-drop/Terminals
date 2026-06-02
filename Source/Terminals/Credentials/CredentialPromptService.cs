using System.Windows.Forms;
using Terminals.Configuration;

namespace Terminals.Credentials
{
    internal sealed class CredentialPromptService : ICredentialPromptService
    {
        public ConnectionCredentialPromptResult PromptForSshConnection(
            IWin32Window owner,
            string host,
            string defaultUserName,
            string defaultPassword,
            string defaultDomain)
        {
            using (var form = new SshConnectCredentialForm(host, defaultUserName, defaultPassword))
            {
                if (form.ShowDialog(owner) != DialogResult.OK)
                    return ConnectionCredentialPromptResult.Cancelled();

                if (string.IsNullOrWhiteSpace(form.UserName))
                    return ConnectionCredentialPromptResult.Cancelled();

                return ConnectionCredentialPromptResult.FromCredentials(
                    form.UserName,
                    form.Password,
                    defaultDomain);
            }
        }
    }
}
