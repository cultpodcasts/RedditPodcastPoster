using CommandLine;

namespace Discover;

public class DiscoveryRequest
{
    [Value(0, MetaName = "number-of-hours", HelpText = "The number of hours to search within", Required = false,
        Default = 12)]
    public int NumberOfHours { get; set; }

    [Option('l', "listen-notes", Default = false, HelpText = "Search Listen Notes")]
    public bool IncludeListenNotes { get; set; }

    [Option('s', "spotify", Default = false, HelpText = "Search Spotify")]
    public bool ExcludeSpotify { get; set; }

    [Option('y', "youtube", Default = false, HelpText = "Search Listen Notes")]
    public bool IncludeYouTube { get; set; }
}