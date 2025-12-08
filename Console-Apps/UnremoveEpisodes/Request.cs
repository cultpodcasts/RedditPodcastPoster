using CommandLine;

namespace UnremoveEpisodes;

public class Request
{
    [Value(0, MetaName = "filename", HelpText = "Filename containing episodes to restore", Required = true)]
    public required string Filename { get; set; }
}