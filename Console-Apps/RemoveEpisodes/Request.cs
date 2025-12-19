using CommandLine;

namespace RemoveEpisodes;

public class Request
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