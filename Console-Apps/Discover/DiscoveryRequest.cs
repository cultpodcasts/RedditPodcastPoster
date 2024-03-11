using CommandLine;

namespace Discover;

public class DiscoveryRequest
{
    [Value(0, MetaName = "number-of-hours", HelpText = "The number of hours to search within", Required = false,
        Default = 12)]
    public int NumberOfHours { get; set; }

    [Option('l', "include-listen-notes", Default = false, HelpText = "Search Listen Notes")]
    public bool IncludeListenNotes { get; set; }

    [Option('s', "exclude-spotify", Default = false, HelpText = "Exclude Spotify")]
    public bool ExcludeSpotify { get; set; }

    [Option('y', "include-youtube", Default = false, HelpText = "Search Listen Notes")]
    public bool IncludeYouTube { get; set; }
}