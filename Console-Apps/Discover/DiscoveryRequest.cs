using CommandLine;

namespace Discover;

public class DiscoveryRequest
{
    [Option('r', "number-of-hours", HelpText = "The number of hours to search within", Required = false,
        SetName = "since")]
    public int? NumberOfHours { get; set; }

    [Option('t', "time-since", HelpText = "Discover items released sine this time", Required = false,
        SetName = "since")]
    public DateTime? Since { get; set; }

    [Option('l', "include-listen-notes", Default = false, HelpText = "Search Listen Notes")]
    public bool IncludeListenNotes { get; set; }

    [Option('d', "include-taddy", Default = false, HelpText = "Search Taddy")]
    public bool IncludeTaddy { get; set; }

    [Option('s', "exclude-spotify", Default = false, HelpText = "Exclude Spotify")]
    public bool ExcludeSpotify { get; set; }

    [Option('y', "include-youtube", Default = false, HelpText = "Search Listen Notes")]
    public bool IncludeYouTube { get; set; }

    [Option('e', "enrich-listennotes-from-spotify", Default = true, HelpText = "Enrich Listennotes from Spotify")]
    public bool EnrichFromSpotify { get; set; }

    [Option('u', "use-remote", Default = false, HelpText = "Use Remotely Collected Data")]
    public bool UseRemote { get; set; }

    [Option('a', "enrich-spotify-from-apple", Default = true, HelpText = "Enrich Spotify from Apple")]
    public bool EnrichFromApple { get; set; }
}