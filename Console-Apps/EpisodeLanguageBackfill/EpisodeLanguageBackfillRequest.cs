using CommandLine;

namespace EpisodeLanguageBackfill;

public class EpisodeLanguageBackfillRequest
{
    [Option('a', "apply", Required = false, Default = false,
        HelpText = "Apply changes. Without this flag the run is a dry-run report only.")]
    public bool Apply { get; set; }
}
