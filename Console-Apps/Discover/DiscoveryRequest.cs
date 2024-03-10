using CommandLine;

namespace Discover;

public class DiscoveryRequest
{
    [Value(0, MetaName = "number-of-days", HelpText = "The number of days to search within", Required = false,
        Default = 1)]
    public int NumberOfDays { get; set; }

    [Option('l', "listen-notes", Default = false, HelpText = "Search Listen Notes")]
    public bool IncludeListenNotes { get; set; }

    [Option('s', "spotify", Default = true, HelpText = "Search Spotify")]
    public bool IncludeSpotify { get; set; }

    [Option('y', "youtube", Default = false, HelpText = "Search Listen Notes")]
    public bool IncludeYouTube { get; set; }
}