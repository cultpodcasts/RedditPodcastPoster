using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace Api.Services.Films;

/// <summary>Builds a <see cref="FilmTrailer"/> from the podcast episode being attached.</summary>
public static class FilmTrailerFactory
{
    private static readonly HashSet<string> AudioOnlyKeys = new(StringComparer.Ordinal)
    {
        ServiceKeys.Spotify,
        ServiceKeys.Apple
    };

    private static readonly string[] PodcastPlatformKeys =
    [
        ServiceKeys.YouTube,
        ServiceKeys.Spotify,
        ServiceKeys.Apple
    ];

    /// <summary>
    /// The trailer plays from the episode's YouTube link when it has one, otherwise from its first video link (any
    /// key but the audio-only Spotify and Apple Podcasts), otherwise from an audio link; ties go by service key, so
    /// the choice is stable. Null when the episode has no link at all. Also stores the source episode's YouTube,
    /// Spotify and Apple URLs and platform ids so every platform still matches after attach.
    /// </summary>
    public static FilmTrailer? From(Episode episode, string? title)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var url = PlaybackUrl(episode);
        if (url is null)
        {
            return null;
        }

        return new FilmTrailer
        {
            Id = episode.Id,
            Title = string.IsNullOrWhiteSpace(title) ? episode.Title.Trim() : title.Trim(),
            Url = url,
            Image = EpisodeServicePresence.CoalescedImage(episode, StreamingServiceCatalog.ImageCoalesceOrder),
            Release = episode.ReleaseUtc == default ? null : episode.ReleaseUtc,
            Length = episode.Length,
            Ids = CopyPodcastPlatformIds(episode),
            Services = CopyPodcastPlatformServices(episode)
        };
    }

    private static Uri? PlaybackUrl(Episode episode)
    {
        if (episode.Services is not { Count: > 0 } services)
        {
            return null;
        }

        if (services.TryGetValue(ServiceKeys.YouTube, out var youTube) && youTube.Url is not null)
        {
            return youTube.Url;
        }

        return services
            .Where(x => x.Value.Url is not null)
            .OrderBy(x => AudioOnlyKeys.Contains(x.Key))
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Value.Url)
            .FirstOrDefault();
    }

    private static EpisodeIds? CopyPodcastPlatformIds(Episode episode)
    {
        var spotify = EpisodeServicePresence.SpotifyEpisodeId(episode);
        var apple = EpisodeServicePresence.AppleEpisodeId(episode);
        var youTube = EpisodeServicePresence.YouTubeEpisodeId(episode);
        if (spotify is null && apple is null && youTube is null)
        {
            return null;
        }

        return new EpisodeIds
        {
            Spotify = spotify,
            Apple = apple,
            YouTube = youTube
        };
    }

    private static Dictionary<string, ServiceLink>? CopyPodcastPlatformServices(Episode episode)
    {
        Dictionary<string, ServiceLink>? services = null;
        foreach (var key in PodcastPlatformKeys)
        {
            var url = EpisodeServicePresence.TryGetUrl(episode, key);
            if (url is null)
            {
                continue;
            }

            services ??= new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
            services[key] = new ServiceLink { Url = url };
        }

        return services;
    }
}
