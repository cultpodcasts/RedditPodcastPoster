using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Search.Indexing;

/// <summary>
/// The search document's <c>spotifyId</c>, <c>appleId</c> and <c>podcastAppleId</c> for a playable that stores no
/// <c>ids</c> object (TV episode, news report, film), read from its <c>services</c> URLs. These are the C# twins of
/// <see cref="PlayableSearchSql.SpotifyEpisodeIdFromUrl"/>, <see cref="PlayableSearchSql.AppleEpisodeIdFromUrl"/> and
/// <see cref="PlayableSearchSql.ApplePodcastIdFromUrlExpression"/>, so a curator-edit push
/// (<c>CataloguePlayableSearchDocument.For</c>) writes the same values the pull indexer does and a merge-upload
/// never blanks them. They use the same string rules as the SQL (not the provider resolvers), so the two paths
/// cannot drift apart.
/// </summary>
public static class PlayableServiceSearchIds
{
    private const string AppleEpisodeMarker = "?i=";

    /// <summary>The three ids from <paramref name="services"/>; each is null when its URL gives none.</summary>
    public static (string? SpotifyId, string? AppleId, string? PodcastAppleId) From(
        IReadOnlyDictionary<string, ServiceLink>? services)
    {
        var spotifyUrl = UrlOf(services, ServiceKeys.Spotify);
        var appleUrl = UrlOf(services, ServiceKeys.Apple);
        return (SpotifyEpisodeId(spotifyUrl), AppleEpisodeId(appleUrl), ApplePodcastId(appleUrl));
    }

    /// <summary>Text after <c>https://open.spotify.com/episode/</c>, up to any <c>?</c>.</summary>
    public static string? SpotifyEpisodeId(string? url)
    {
        var prefix = PlayableSearchSql.SpotifyEpisodeUrlPrefix;
        if (url is null || !url.StartsWith(prefix, StringComparison.Ordinal) || url.Length <= prefix.Length)
        {
            return null;
        }

        var query = url.IndexOf('?');
        if (query == -1)
        {
            return url[prefix.Length..];
        }

        return query > prefix.Length ? url[prefix.Length..query] : null;
    }

    /// <summary>Text after <c>?i=</c>, up to any later <c>&amp;</c>.</summary>
    public static string? AppleEpisodeId(string? url)
    {
        var marker = url?.IndexOf(AppleEpisodeMarker, StringComparison.Ordinal) ?? -1;
        if (url is null || marker == -1)
        {
            return null;
        }

        var start = marker + AppleEpisodeMarker.Length;
        if (url.Length <= start)
        {
            return null;
        }

        var amp = url.IndexOf('&', start);
        if (amp == -1)
        {
            return url[start..];
        }

        return amp > start ? url[start..amp] : null;
    }

    /// <summary>Text of the last <c>/id</c> segment before <c>?i=</c>.</summary>
    public static string? ApplePodcastId(string? url)
    {
        var marker = url?.IndexOf(AppleEpisodeMarker, StringComparison.Ordinal) ?? -1;
        if (url is null || marker == -1)
        {
            return null;
        }

        var beforeEpisode = url[..marker];
        var segment = beforeEpisode.LastIndexOf("/id", StringComparison.Ordinal);
        if (segment == -1)
        {
            return null;
        }

        var start = segment + "/id".Length;
        return start < beforeEpisode.Length ? beforeEpisode[start..] : null;
    }

    private static string? UrlOf(IReadOnlyDictionary<string, ServiceLink>? services, string key) =>
        services is not null && services.TryGetValue(key, out var link) ? link.Url?.OriginalString : null;
}
