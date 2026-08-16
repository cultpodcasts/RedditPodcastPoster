using CommandLine;

namespace MigrateConfig;

[Verb("secrets", HelpText = "Convert user-secrets JSON to Azure function app-setting JSON.")]
public class SecretsRequest
{
    [Value(0, MetaName = "secrets-json-path", Required = true, HelpText = "Path to user-secrets JSON file.")]
    public string Path { get; set; } = "";

    [Option("version", HelpText = "Display version information")]
    public bool Version { get; set; }
}
