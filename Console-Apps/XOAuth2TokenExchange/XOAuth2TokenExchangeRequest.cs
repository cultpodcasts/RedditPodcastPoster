using CommandLine;

namespace XOAuth2TokenExchange;

public class XOAuth2TokenExchangeRequest
{
    [Option("execute", Required = false, Default = false,
        HelpText = "Send the token exchange to X and save the new OAuth 2.0 pair to LookUps. Without it the command is a dry run: it checks settings and the saved state and sends nothing.")]
    public bool Execute { get; set; }
}
