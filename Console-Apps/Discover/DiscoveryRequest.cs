using System.Globalization;
using CommandLine;

namespace Discover;

public class DiscoveryRequest
{
    public static readonly TimeSpan DefaultTaddyOffset = TimeSpan.FromHours(2);

    private const string DefaultTaddyOffsetOptionValue = "02:00:00";
    [Option('r', "number-of-hours",
        HelpText = "Search window as a timespan (e.g. 6:10:00) or whole hours (e.g. 7)", Required = false,
        SetName = "since")]
    public string? SearchWindow { get; set; }

    [Option('t', "time-since", HelpText = "Discover items released sine this time", Required = false,
        SetName = "since")]
    public DateTime? Since { get; set; }

    [Option('l', "include-listen-notes", Default = true, HelpText = "Search Listen Notes")]
    public bool IncludeListenNotes { get; set; }

    [Option('d', "include-taddy", Default = true, HelpText = "Search Taddy")]
    public bool IncludeTaddy { get; set; }

    [Option('s', "exclude-spotify", Default = false, HelpText = "Exclude Spotify")]
    public bool ExcludeSpotify { get; set; }

    [Option('y', "include-youtube", Default = true, HelpText = "Search YouTube")]
    public bool IncludeYouTube { get; set; }

    [Option('e', "enrich-listennotes-from-spotify", Default = true, HelpText = "Enrich Listennotes from Spotify")]
    public bool EnrichFromSpotify { get; set; }

    [Option('u', "use-remote", Default = false, HelpText = "Use Remotely Collected Data")]
    public bool UseRemote { get; set; }

    [Option('a', "enrich-spotify-from-apple", Default = true, HelpText = "Enrich Spotify from Apple")]
    public bool EnrichFromApple { get; set; }

    [Option('o', "taddy-offset", Default = DefaultTaddyOffsetOptionValue,
        HelpText = "The amount of time extra to search against Taddy due to their indexing delay of 2 hours")]
    public string? TaddyOffset { get; set; }

    public TimeSpan GetTaddyOffset() =>
        string.IsNullOrWhiteSpace(TaddyOffset)
            ? DefaultTaddyOffset
            : TimeSpan.Parse(TaddyOffset, CultureInfo.InvariantCulture);
}