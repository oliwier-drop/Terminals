namespace Terminals.Configuration
{
    public interface ICredentialPromptConsumer
    {
        ICredentialPromptService CredentialPromptService { get; set; }
    }
}
