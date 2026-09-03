using CommandLine;

namespace CreateSearchIndex;

public class CreateSearchIndexRequest
{
    [Option('i', "index", Required = false, Default = null, HelpText = "Index name")]
    public string? IndexName { get; set; }

    [Option('t', "teardown-index", Required = false, Default = false, HelpText = "Tear-Down Index")]
    public bool TearDownIndex { get; set; }

    [Option("update-existing", Required = false, Default = false,
        HelpText = "Add missing index fields (e.g. svc) and upsert the Cosmos data-source query. Refuses --teardown-index.")]
    public bool UpdateExisting { get; set; }

    [Option("reset-indexer", Required = false, Default = false,
        HelpText = "Reset the named indexer high-water mark so existing documents pick up new fields. Use with --update-existing.")]
    public bool ResetIndexer { get; set; }

    [Option('d', "datasource", Required = false, Default = null, HelpText = "Data-source name")]
    public string? DataSourceName { get; set; }

    [Option('x', "indexer", Required = false, Default = null, HelpText = "Indexer name")]
    public string? IndexerName { get; set; }

    [Option('r', "run-indexer", Required = false, Default = false, HelpText = "Run the indexer")]
    public bool RunIndexer { get; set; }

    [Option(shortName:'m', "run-indexer-max-attempts", Required = false, Default = 10, HelpText = "Max automated rerun attempts when indexer times out")]
    public int RunIndexerMaxAttempts { get; set; }

    [Option(shortName: 'p', "run-indexer-poll-seconds", Required = false, Default = 10, HelpText = "Polling interval in seconds when monitoring indexer execution")]
    public int RunIndexerPollSeconds { get; set; }

    [Option(shortName: 'b', "not-break-on-duplicates", Required = false, Default = true, HelpText = "Do not break the indexer run if duplicates are found")]
    public bool NotBreakOnDuplicates { get; set; }

    [Option(shortName: 'w', "run-indexer-max-wait-seconds", Required = false, Default = 30, HelpText = "Maximum seconds to wait for a single indexer run before treating it as a retryable stall")]
    public int RunIndexerMaxWaitSeconds { get; set; }

    [Option("version", HelpText = "Display version information")]
    public bool Version { get; set; }
}