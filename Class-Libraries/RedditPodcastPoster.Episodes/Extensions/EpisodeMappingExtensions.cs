using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Extensions;

public static class EpisodeMappingExtensions
{
    public static EpisodeCandidate ToCandidate(this Episode episode, Service sourceService)
    {
        PlatformLink? sourceLink = sourceService switch
        {
            Service.Spotify => CreatePlatformLink(
                Service.Spotify,
                episode.SpotifyId,
                episode.Urls.Spotify,
                episode.Images?.Spotify),
            Service.Apple => CreatePlatformLink(
                Service.Apple,
                episode.AppleId?.ToString(),
                episode.Urls.Apple,
                episode.Images?.Apple),
            Service.YouTube => CreatePlatformLink(
                Service.YouTube,
                episode.YouTubeId,
                episode.Urls.YouTube,
                episode.Images?.YouTube),
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
                episode.SpotifyId,
                episode.Urls.Spotify,
                episode.Images?.Spotify),
            episode.Description,
            new ReleaseInfo(episode.Release, ReleasePrecision.DateTimeUtc));

    public static EpisodePlatformPatch ToApplePatch(this Episode episode) =>
        new(
            CreatePlatformLink(
                Service.Apple,
                episode.AppleId?.ToString(),
                episode.Urls.Apple,
                episode.Images?.Apple),
            episode.Description,
            new ReleaseInfo(episode.Release, ReleasePrecision.DateTimeUtc));

    public static EpisodePlatformPatch ToYouTubePatch(this Episode episode) =>
        new(
            CreatePlatformLink(
                Service.YouTube,
                episode.YouTubeId,
                episode.Urls.YouTube,
                episode.Images?.YouTube),
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
