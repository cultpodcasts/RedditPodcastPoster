using CommandLine;

namespace Sqllite3DatabasePublisher;

public class Request
{
    [Value(0, HelpText = "Database Name", Required = true)]
    public required string DatabaseName { get; set; }
}