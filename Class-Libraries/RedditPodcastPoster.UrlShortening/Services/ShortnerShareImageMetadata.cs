using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.UrlShortening.Services;

/// <summary>
/// Builds optional share-image fields for new shortener KV metadata
/// (search-index encoding + twitter card aspect). Compaction mirrors
/// <c>SearchEpisodeImage</c> / website <c>expandImage</c>.
/// </summary>
public static class ShortnerShareImageMetadata
{
    private const string YouTubePrefix = "https://i.ytimg.com/vi/";
    private const string SpotifyPrefix = "https://i.scdn.co/image/";
    private const string AppleScheme = "https://is";
    private const string AppleHostTail = "-ssl.mzstatic.com/image/thumb/";

    private static readonly (string FileName, char Code)[] YouTubeQualities =
    [
        ("maxresdefault.jpg", 'x'),
        ("sddefault.jpg", 's'),
        ("hqdefault.jpg", 'h'),
        ("mqdefault.jpg", 'm'),
        ("default.jpg", 'd')
    ];

    public static void Apply(MetaData metadata, Episode episode)
    {
        var youTubeId = string.IsNullOrWhiteSpace(episode.YouTubeId) ? null : episode.YouTubeId;
        var image = CompactFromEpisode(episode.Images, youTubeId);
        if (string.IsNullOrEmpty(image))
        {
            return;
        }

        metadata.Image = image;
        metadata.YoutubeId = youTubeId;
        metadata.ImageAspect = ResolveAspect(episode, image, youTubeId);
    }

    /// <summary>
    /// Resolves KV <c>imageAspect</c> for twitter:card selection.
    /// </summary>
    /// <param name="episode">The episode whose platform URLs may force wide aspect.</param>
    /// <param name="imageTokenOrUrl">The compacted or absolute image reference.</param>
    /// <param name="youTubeId">An optional YouTube id that implies wide thumbnails.</param>
    /// <returns>One of the enumeration values that specifies wide or square art.</returns>
    public static ShareImageAspect ResolveAspect(Episode episode, string imageTokenOrUrl, string? youTubeId)
    {
        if (IsYouTubeThumb(imageTokenOrUrl) || !string.IsNullOrWhiteSpace(youTubeId))
        {
            return ShareImageAspect.Wide;
        }

        if (IsBbcIplayer(episode.Urls.BBC) || episode.Urls.InternetArchive is not null)
        {
            return ShareImageAspect.Wide;
        }

        return ShareImageAspect.Square;
    }

    /// <summary>Same selection as SearchEpisodeImage.From: youtube ?? spotify ?? apple ?? other, then compact.</summary>
    private static string? CompactFromEpisode(EpisodeImages? images, string? youTubeId)
    {
        var selected = images?.YouTube ?? images?.Spotify ?? images?.Apple ?? images?.Other;
        if (selected is null)
        {
            return null;
        }

        var url = selected.ToString();
        return Compact(url, youTubeId) ?? url;
    }

    private static string? Compact(string url, string? youTubeId)
    {
        var token = TryYouTube(url, youTubeId) ?? TrySpotify(url) ?? TryApple(url);
        return token is not null && Expand(token, youTubeId) == url ? token : null;
    }

    private static string Expand(string image, string? youTubeId)
    {
        if (string.IsNullOrEmpty(image) || image.StartsWith("http", StringComparison.Ordinal))
        {
            return image;
        }

        var payload = image[1..];
        switch (image[0])
        {
            case 'y':
                foreach (var (fileName, code) in YouTubeQualities)
                {
                    if (payload.Length == 1 && payload[0] == code && !string.IsNullOrWhiteSpace(youTubeId))
                    {
                        return $"{YouTubePrefix}{youTubeId}/{fileName}";
                    }
                }

                return image;
            case 's' when payload.Length > 0:
                return $"{SpotifyPrefix}{payload}";
            case 'a' when payload.Length >= 1:
                return $"{AppleScheme}{payload[0]}{AppleHostTail}{payload[1..]}";
            default:
                return image;
        }
    }

    private static string? TryYouTube(string url, string? youTubeId)
    {
        if (string.IsNullOrWhiteSpace(youTubeId))
        {
            return null;
        }

        var prefix = $"{YouTubePrefix}{youTubeId}/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var fileName = url[prefix.Length..];
        foreach (var (name, code) in YouTubeQualities)
        {
            if (fileName == name)
            {
                return $"y{code}";
            }
        }

        return null;
    }

    private static string? TrySpotify(string url)
    {
        if (!url.StartsWith(SpotifyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var id = url[SpotifyPrefix.Length..];
        if (id.Length == 0 || id.Contains('/') || id.Contains('?') || id.Contains('#'))
        {
            return null;
        }

        return $"s{id}";
    }

    private static string? TryApple(string url)
    {
        if (!url.StartsWith(AppleScheme, StringComparison.Ordinal) || url.Length <= AppleScheme.Length)
        {
            return null;
        }

        var digit = url[AppleScheme.Length];
        if (digit is < '1' or > '5')
        {
            return null;
        }

        var afterDigit = url[(AppleScheme.Length + 1)..];
        if (!afterDigit.StartsWith(AppleHostTail, StringComparison.Ordinal))
        {
            return null;
        }

        var path = afterDigit[AppleHostTail.Length..];
        return $"a{digit}{path}";
    }

    private static bool IsYouTubeThumb(string image)
    {
        if (image.Length >= 2 && image[0] == 'y' && "xshmd".Contains(image[1]))
        {
            return true;
        }

        return Uri.TryCreate(image, UriKind.Absolute, out var url) &&
               url.Host.Equals("i.ytimg.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBbcIplayer(Uri? bbc)
    {
        if (bbc is null)
        {
            return false;
        }

        var host = bbc.Host;
        var isBbc = host.EndsWith("bbc.com", StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("bbc.co.uk", StringComparison.OrdinalIgnoreCase);
        if (!isBbc)
        {
            return false;
        }

        var path = bbc.AbsolutePath;
        return path.StartsWith("/iplayer/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/news/av-embeds/", StringComparison.OrdinalIgnoreCase);
    }
}
