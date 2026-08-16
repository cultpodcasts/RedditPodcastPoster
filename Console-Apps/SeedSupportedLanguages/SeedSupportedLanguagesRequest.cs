using CommandLine;

namespace SeedSupportedLanguages;

public class SeedSupportedLanguagesRequest
{
    [Option('a', "apply", Required = false, Default = false,
        HelpText = "Write SupportedLanguagesConfig to LookUps. Without this flag the run is a dry-run report only.")]
    public bool Apply { get; set; }

    [Option("from-r2", Required = false, Default = false,
        HelpText = "Build the document from the current R2 languages object (authority) instead of CreateDefault().")]
    public bool FromR2 { get; set; }

    [Option('f', "force", Required = false, Default = false,
        HelpText = "Overwrite an existing LookUps document.")]
    public bool Force { get; set; }


    [Option("version", HelpText = "Display version information")]
    public bool Version { get; set; }
}
