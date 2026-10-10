using CommandLine;

namespace RemoveEpisodes;

[Verb("remove", isDefault: true, HelpText = "Mark matching search episodes as removed.")]
public class RemoveRequest
{
    [Value(0, MetaName = "query", HelpText = "Query to perform", Required = true)]
    public required string Query { get; set; }

    [Value(1, MetaName = "throttle", HelpText = "Max permitted episodes to remove", Default = 5)]
    public required int Throttle { get; set; }

    [Option('n', "not-whole-term", Default = false, HelpText = "Do not treat query as a quoted term.")]
    public bool NotWholeTerm { get; set; }

    [Option('r', "non-dry-run", Default = false, HelpText = "Persist changes to database.")]
    public bool IsNonDryRun { get; set; }
}

[Verb("restore", HelpText = "Restore removed episodes listed in a log file (unremove).")]
public class RestoreRequest
{
    [Value(0, MetaName = "filename", HelpText = "Filename containing episodes to restore", Required = true)]
    public required string Filename { get; set; }
}

[Verb("restore-podcast",
    HelpText = "Undo an accidental podcast removal: un-remove the podcast, clear parentRemoved on its episodes, " +
               "re-index and re-create short URLs for episodes that were not themselves removed. Dry run unless --non-dry-run.")]
public class RestorePodcastRequest
{
    [Option('i', "podcast-id", Separator = ',', HelpText = "Podcast id(s), comma separated.")]
    public IEnumerable<Guid> PodcastIds { get; set; } = [];

    [Option('p', "podcast-name", Separator = '|',
        HelpText = "Exact podcast name(s), '|' separated. Each must match exactly one podcast.")]
    public IEnumerable<string> PodcastNames { get; set; } = [];

    [Option("skip-shortner", Default = false, HelpText = "Do not re-create short-URL (Cloudflare KV) keys.")]
    public bool SkipShortner { get; set; }

    [Option('r', "non-dry-run", Default = false, HelpText = "Persist changes (Cosmos, search index, short URLs).")]
    public bool IsNonDryRun { get; set; }
}
