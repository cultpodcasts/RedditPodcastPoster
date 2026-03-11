using CommandLine;

namespace CreateSearchIndex;

public class CreateSearchIndexRequest
{
    [Option('i', "index", Required = false, Default = null, HelpText = "Index name")]
    public string? IndexName { get; set; }

    [Option('t', "teardown-index", Required = false, Default = false, HelpText = "Tear-Down Index")]
    public bool TearDownIndex { get; set; }

    [Option('d', "datasource", Required = false, Default = null, HelpText = "Data-source name")]
    public string? DataSourceName { get; set; }

    [Option('x', "indexer", Required = false, Default = null, HelpText = "Indexer name")]
    public string? IndexerName { get; set; }

    [Option('r', "run-indexer", Required = false, Default = false, HelpText = "Run the indexer")]
    public bool RunIndexer { get; set; }

    [Option("run-indexer-max-attempts", Required = false, Default = 10,
        HelpText = "Max automated rerun attempts when indexer times out")]
    public int RunIndexerMaxAttempts { get; set; }

    [Option("run-indexer-poll-seconds", Required = false, Default = 10,
        HelpText = "Polling interval in seconds when monitoring indexer execution")]
    public int RunIndexerPollSeconds { get; set; }
}