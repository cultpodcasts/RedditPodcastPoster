using System.Globalization;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace CatalogueMigrate;

/// <summary>A playable already stored on the destination, with the service links the mapper copied.</summary>
public sealed record CatalogueMigrateStoredPlayable(
    Guid Id,
    IReadOnlyDictionary<string, ServiceLink>? Services);

/// <summary>
/// A re-created episode (new id, same YouTube / Spotify / Apple service id) must not become a second playable.
/// The same episode id is an upsert, not a duplicate.
/// </summary>
public static class CatalogueMigrateHeldPlayables
{
    public static bool AlreadyHeld(Episode episode, IReadOnlyList<CatalogueMigrateStoredPlayable> stored)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(stored);
        if (stored.Any(playable => playable.Id == episode.Id))
        {
            return false;
        }

        var incoming = ServiceIds(episode);
        if (incoming.Count == 0)
        {
            return false;
        }

        return stored.Any(playable => ServiceIds(playable.Services).Overlaps(incoming));
    }

    private static HashSet<ServiceId> ServiceIds(Episode episode)
    {
        var ids = ServiceIds(episode.Services);
        Add(ids, ServiceKeys.YouTube, EpisodeServicePresence.YouTubeEpisodeId(episode));
        Add(ids, ServiceKeys.Spotify, EpisodeServicePresence.SpotifyEpisodeId(episode));
        Add(ids, ServiceKeys.Apple, EpisodeServicePresence.AppleEpisodeId(episode)?.ToString(CultureInfo.InvariantCulture));
        return ids;
    }

    private static HashSet<ServiceId> ServiceIds(IReadOnlyDictionary<string, ServiceLink>? services)
    {
        var ids = new HashSet<ServiceId>();
        if (services is not { Count: > 0 })
        {
            return ids;
        }

        AddUrl(ids, ServiceKeys.YouTube, Url(services, ServiceKeys.YouTube), YouTubeIdResolver.Extract);
        AddUrl(ids, ServiceKeys.Spotify, Url(services, ServiceKeys.Spotify), SpotifyIdResolver.GetEpisodeId);
        if (Url(services, ServiceKeys.Apple) is { } apple)
        {
            Add(ids, ServiceKeys.Apple, AppleIdResolver.GetEpisodeId(apple)?.ToString(CultureInfo.InvariantCulture));
        }

        return ids;
    }

    private static void AddUrl(
        HashSet<ServiceId> ids,
        string service,
        Uri? url,
        Func<Uri, string?> extract)
    {
        if (url is null)
        {
            return;
        }

        Add(ids, service, extract(url));
    }

    private static Uri? Url(IReadOnlyDictionary<string, ServiceLink> services, string key) =>
        services.TryGetValue(key, out var link) ? link.Url : null;

    private static void Add(HashSet<ServiceId> ids, string service, string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            ids.Add(new ServiceId(service, id));
        }
    }

    private readonly record struct ServiceId(string Service, string Id);
}
