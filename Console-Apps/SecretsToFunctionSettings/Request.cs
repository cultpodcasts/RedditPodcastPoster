using CommandLine;

namespace SecretsToFunctionSettings;

internal class Request
{
    [Value(0, MetaName = "secrets-location", HelpText = "Location of the secrets file", Required = true)]
    public required string Path { get; set; }
}