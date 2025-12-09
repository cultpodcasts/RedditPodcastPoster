using CommandLine;

namespace AddSubjectToSearchMatches;

public class Request
{
    [Value(0, MetaName = "query", HelpText = "Query to perform", Required = true)]
    public required string Query { get; set; }

    [Value(1, MetaName = "throttle", HelpText = "Max permitted episodes to update", Default = 5)]
    public required int Throttle { get; set; }

    [Option('s', "add-subject-when-not-subject-match", Default = false, HelpText = "Bypass subject-matching on episode; add subject if is a search-result.")]
    public bool AddSubjectWhenNotSubjectMatch { get; set; }

    [Option('n', "not-whole-term", Default = false, HelpText = "Do not treat query as a quoted term.")]
    public bool NotWholeTerm { get; set; }

    [Option('d', "dry-run", Default = false, HelpText = "Do not persist changes to database.")]
    public bool IsDryRun { get; set; }

}