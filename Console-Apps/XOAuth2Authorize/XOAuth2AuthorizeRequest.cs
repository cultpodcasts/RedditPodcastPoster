using CommandLine;

namespace XOAuth2Authorize;

public class XOAuth2AuthorizeRequest
{
    [Option("execute", Required = false, Default = false,
        HelpText = "Open the X login and save the new OAuth 2.0 pair to LookUps. Without it the command is a dry run and sends nothing.")]
    public bool Execute { get; set; }

    [Option("redirect-uri", Required = false, Default = "http://127.0.0.1:8739/callback",
        HelpText = "Loopback redirect registered on the X app. http://127.0.0.1 or http://localhost, with a port and no query.")]
    public string RedirectUri { get; set; } = "http://127.0.0.1:8739/callback";

    [Option("manual", Required = false, Default = false,
        HelpText = "Do not listen on the redirect. Print the URL and read the pasted callback URL (or code) from this console.")]
    public bool Manual { get; set; }
}
