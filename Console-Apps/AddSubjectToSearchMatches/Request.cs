using CommandLine;

namespace AddSubjectToSearchMatches;

public class Request
{
    [Value(0, MetaName = "query", HelpText = "Query to perform", Required = true)]
    public required string Query { get; set; }
}