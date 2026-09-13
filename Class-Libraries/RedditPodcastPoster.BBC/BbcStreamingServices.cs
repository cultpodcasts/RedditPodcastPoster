using RedditPodcastPoster.BBC.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.BBC;

public static class BbcSoundsStreamingService
{
    public const string Key = StreamingServiceKeys.BbcSounds;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "BBC Sounds",
        "bbc-sounds",
        false,
        ["bbc.co.uk", "bbc.com"],
        tryResolve: url => BBCUrlMatcher.IsSoundsCatalogUrl(url) ? Key : null,
        tryCompact: BBCUrlMatcher.TrySoundsCompactPayload,
        tryExpand: BBCUrlMatcher.TryExpandSoundsPayload);
}

public static class BbcIplayerStreamingService
{
    public const string Key = StreamingServiceKeys.BbcIplayer;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "BBC iPlayer",
        "bbc-iplayer",
        true,
        ["bbc.co.uk", "bbc.com"],
        tryResolve: url => BBCUrlMatcher.IsIplayerCatalogUrl(url) ? Key : null,
        tryCompact: BBCUrlMatcher.TryIplayerCompactPayload,
        tryExpand: BBCUrlMatcher.TryExpandIplayerPayload);
}
