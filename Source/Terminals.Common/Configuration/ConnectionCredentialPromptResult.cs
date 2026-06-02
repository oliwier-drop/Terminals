namespace Terminals.Configuration
{
    public sealed class ConnectionCredentialPromptResult
    {
        public bool Success { get; private set; }

        public string UserName { get; private set; }

        public string Password { get; private set; }

        public string Domain { get; private set; }

        public static ConnectionCredentialPromptResult Cancelled()
        {
            return new ConnectionCredentialPromptResult { Success = false };
        }

        public static ConnectionCredentialPromptResult FromCredentials(string userName, string password, string domain)
        {
            return new ConnectionCredentialPromptResult
            {
                Success = true,
                UserName = userName ?? string.Empty,
                Password = password ?? string.Empty,
                Domain = domain ?? string.Empty
            };
        }
    }
}
