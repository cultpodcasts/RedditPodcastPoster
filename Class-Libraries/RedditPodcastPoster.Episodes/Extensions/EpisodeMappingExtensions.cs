using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Extensions;

public static class EpisodeMappingExtensions
{
    public static EpisodeCandidate ToCandidate(this Episode episode, Service sourceService)
    {
        PlatformLink? sourceLink = sourceService switch
        {
            Service.Spotify => CreatePlatformLink(
                Service.Spotify,
                EpisodeServicePresence.SpotifyEpisodeId(episode),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Spotify)),
            Service.Apple => CreatePlatformLink(
                Service.Apple,
                EpisodeServicePresence.AppleEpisodeId(episode)?.ToString(),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Apple)),
            Service.YouTube => CreatePlatformLink(
                Service.YouTube,
                EpisodeServicePresence.YouTubeEpisodeId(episode),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube)),
            _ => null
        };

        return new EpisodeCandidate(
            episode.Title,
            episode.Description,
            episode.Length,
            new ReleaseInfo(episode.Release, ReleasePrecision.DateTimeUtc),
            sourceLink);
    }

    public static EpisodePlatformPatch ToPlatformPatch(this Episode episode) =>
        new(null, episode.Description, new ReleaseInfo(episode.Release, ReleasePrecision.DateTimeUtc));

    public static EpisodePlatformPatch ToSpotifyPatch(this Episode episode) =>
        new(
            CreatePlatformLink(
                Service.Spotify,
                EpisodeServicePresence.SpotifyEpisodeId(episode),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Spotify)),
            episode.Description,
            new ReleaseInfo(episode.Release, ReleasePrecision.DateTimeUtc));

    public static EpisodePlatformPatch ToApplePatch(this Episode episode) =>
        new(
            CreatePlatformLink(
                Service.Apple,
                EpisodeServicePresence.AppleEpisodeId(episode)?.ToString(),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Apple)),
            episode.Description,
            new ReleaseInfo(episode.Release, ReleasePrecision.DateTimeUtc));

    public static EpisodePlatformPatch ToYouTubePatch(this Episode episode) =>
        new(
            CreatePlatformLink(
                Service.YouTube,
                EpisodeServicePresence.YouTubeEpisodeId(episode),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube)),
            episode.Description,
            new ReleaseInfo(episode.Release, ReleasePrecision.DateTimeUtc));

    private static PlatformLink? CreatePlatformLink(Service service, string? id, Uri? url, Uri? image)
    {
        if (string.IsNullOrWhiteSpace(id) && url is null && image is null)
        {
            return null;
        }

        return new PlatformLink(service, string.IsNullOrWhiteSpace(id) ? null : id, url, image);
    }
}
