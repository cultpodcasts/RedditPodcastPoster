using CommandLine;

namespace SeedTitleCasingRules;

public class SeedTitleCasingRulesRequest
{
    [Option('a', "apply", Required = false, Default = false,
        HelpText = "Write the English title-casing document. Without this flag the run is a dry-run report only.")]
    public bool Apply { get; set; }

    [Option('l', "language", Required = false, Default = "en",
        HelpText = "Language code to seed (default: en).")]
    public string Language { get; set; } = "en";
}
