namespace RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

/// <summary>
/// JSON keys for streaming catalogue destinations (not Spotify/Apple/YouTube).
/// Kept out of Models so adding a provider does not grow the episode document kernel.
/// </summary>
public static class StreamingServiceKeys
{
    public const string BbcSounds = "bbcSounds";
    public const string BbcIplayer = "bbcIplayer";
    public const string InternetArchive = "internetArchive";
    public const string Vimeo = "vimeo";
    public const string Netflix = "netflix";
    public const string AmazonPrime = "amazonPrime";
    public const string ParamountPlus = "paramountPlus";
    public const string HboMax = "hboMax";
    public const string PlaySuisse = "playSuisse";
    public const string PlayRts = "playRts";
    public const string TvnzPlus = "tvnzPlus";
    public const string Itvx = "itvx";
    public const string Channel4 = "channel4";
    public const string Fawesome = "fawesome";
    public const string DisneyPlus = "disneyPlus";
    public const string DiscoveryPlus = "discoveryPlus";
    public const string BcVideo = "bitchute";
    public const string Tubi = "tubi";
}
