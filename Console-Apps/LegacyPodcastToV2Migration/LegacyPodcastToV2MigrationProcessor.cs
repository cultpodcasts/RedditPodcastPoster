using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using LegacyEpisode = RedditPodcastPoster.Models.Episode;
using LegacyPodcast = RedditPodcastPoster.Models.Podcast;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace LegacyPodcastToV2Migration;

public sealed record LegacyPodcastToV2MigrationResult(
    int PodcastsMigrated,
    int EpisodesMigrated,
    IReadOnlyCollection<Guid> FailedPodcastIds,
    IReadOnlyCollection<Guid> FailedEpisodeIds);

public class LegacyPodcastToV2MigrationProcessor(
    IPodcastRepository legacyPodcastRepository,
    IPodcastRepositoryV2 podcastRepositoryV2,
    IEpisodeRepository episodeRepository,
    ILookupRepository lookupRepository,
    IPushSubscriptionsRepository pushSubscriptionsRepository,
    ISubjectsRepository subjectsRepository,
    IDiscoveryRepository discoveryRepository,
    ILogger<LegacyPodcastToV2MigrationProcessor> logger)
{
    public async Task<LegacyPodcastToV2MigrationResult> Run(CancellationToken cancellationToken = default)
    {
        var podcastsMigrated = 0;
        var episodesMigrated = 0;
        var failedPodcastIds = new List<Guid>();
        var failedEpisodeIds = new List<Guid>();

        // Migrate Podcasts and Episodes
        var legacyPodcasts = await legacyPodcastRepository.GetAll().ToListAsync(cancellationToken);
        var totalPodcasts = legacyPodcasts.Count;
        var totalEpisodes = legacyPodcasts.Sum(p => p.Episodes.Count);
        var migratedEpisodes = 0;

        for (int i = 0; i < legacyPodcasts.Count; i++)
        {
            var legacyPodcast = legacyPodcasts[i];
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var podcastV2 = ToV2Podcast(legacyPodcast);
                await podcastRepositoryV2.Save(podcastV2);
                podcastsMigrated++;
                Console.WriteLine(
                    $"Podcast migration progress: {podcastsMigrated}/{totalPodcasts} ({podcastsMigrated * 100 / totalPodcasts}%)");

                if (legacyPodcast.Episodes.Count > 0)
                {
                    var episodeBatch = legacyPodcast.Episodes.Select(e => ToV2Episode(legacyPodcast, e)).ToArray();
                    try
                    {
                        await episodeRepository.Save(episodeBatch);
                        migratedEpisodes += episodeBatch.Length;
                        episodesMigrated += episodeBatch.Length;
                        Console.WriteLine(
                            $"Episode migration progress: {migratedEpisodes}/{totalEpisodes} ({migratedEpisodes * 100 / (totalEpisodes == 0 ? 1 : totalEpisodes)}%)");
                    }
                    catch (Exception ex)
                    {
                        failedEpisodeIds.AddRange(episodeBatch.Select(x => x.Id));
                        logger.LogError(ex,
                            "Failed to migrate episodes for legacy podcast id '{PodcastId}' with count '{EpisodeCount}'.",
                            legacyPodcast.Id, episodeBatch.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                failedPodcastIds.Add(legacyPodcast.Id);
                failedEpisodeIds.AddRange(legacyPodcast.Episodes.Select(x => x.Id));
                logger.LogError(ex,
                    "Failed to migrate legacy podcast id '{PodcastId}' and its episodes.",
                    legacyPodcast.Id);
            }
        }

        // Migrate Lookup
        var lookupItems = await lookupRepository.GetAllLegacy().ToListAsync(cancellationToken);
        var totalLookup = lookupItems.Count;
        for (int i = 0; i < totalLookup; i++)
        {
            await lookupRepository.Save(lookupItems[i]);
            Console.WriteLine(
                $"Lookup migration progress: {i + 1}/{totalLookup} ({(i + 1) * 100 / (totalLookup == 0 ? 1 : totalLookup)}%)");
        }

        // Migrate PushSubscriptions
        var pushSubscriptions = await pushSubscriptionsRepository.GetAllLegacy().ToListAsync(cancellationToken);
        var totalPush = pushSubscriptions.Count;
        for (int i = 0; i < totalPush; i++)
        {
            await pushSubscriptionsRepository.Save(pushSubscriptions[i]);
            Console.WriteLine(
                $"PushSubscriptions migration progress: {i + 1}/{totalPush} ({(i + 1) * 100 / (totalPush == 0 ? 1 : totalPush)}%)");
        }

        // Migrate Subjects
        var subjects = await subjectsRepository.GetAllLegacy().ToListAsync(cancellationToken);
        var totalSubjects = subjects.Count;
        for (int i = 0; i < totalSubjects; i++)
        {
            await subjectsRepository.Save(subjects[i]);
            Console.WriteLine(
                $"Subjects migration progress: {i + 1}/{totalSubjects} ({(i + 1) * 100 / (totalSubjects == 0 ? 1 : totalSubjects)}%)");
        }

        // Migrate Discovery
        var discoveries = await discoveryRepository.GetAllLegacy().ToListAsync(cancellationToken);
        var totalDiscovery = discoveries.Count;
        for (int i = 0; i < totalDiscovery; i++)
        {
            await discoveryRepository.Save(discoveries[i]);
            Console.WriteLine(
                $"Discovery migration progress: {i + 1}/{totalDiscovery} ({(i + 1) * 100 / (totalDiscovery == 0 ? 1 : totalDiscovery)}%)");
        }

        return new LegacyPodcastToV2MigrationResult(
            PodcastsMigrated: podcastsMigrated,
            EpisodesMigrated: episodesMigrated,
            FailedPodcastIds: failedPodcastIds,
            FailedEpisodeIds: failedEpisodeIds);
    }

    private static V2Podcast ToV2Podcast(LegacyPodcast legacyPodcast)
    {
        return new V2Podcast
        {
            Id = legacyPodcast.Id,
            Name = legacyPodcast.Name,
            Language = legacyPodcast.Language,
            Publisher = legacyPodcast.Publisher,
            Removed = legacyPodcast.Removed,
            SearchTerms = legacyPodcast.SearchTerms,
            SpotifyId = legacyPodcast.SpotifyId,
            AppleId = legacyPodcast.AppleId,
            YouTubeChannelId = legacyPodcast.YouTubeChannelId,
            FileKey = legacyPodcast.FileKey,
            Timestamp = legacyPodcast.Timestamp
        };
    }

    private static V2Episode ToV2Episode(LegacyPodcast legacyPodcast, LegacyEpisode legacyEpisode)
    {
        return new V2Episode
        {
            Id = legacyEpisode.Id,
            PodcastId = legacyPodcast.Id,
            Title = legacyEpisode.Title,
            Description = legacyEpisode.Description,
            Release = legacyEpisode.Release,
            Length = legacyEpisode.Length,
            Explicit = legacyEpisode.Explicit,
            Posted = legacyEpisode.Posted,
            Tweeted = legacyEpisode.Tweeted,
            BlueskyPosted = legacyEpisode.BlueskyPosted,
            Ignored = legacyEpisode.Ignored,
            Removed = legacyEpisode.Removed,
            SpotifyId = legacyEpisode.SpotifyId,
            AppleId = legacyEpisode.AppleId,
            YouTubeId = legacyEpisode.YouTubeId,
            Urls = legacyEpisode.Urls,
            Subjects = legacyEpisode.Subjects ?? [],
            SearchTerms = legacyEpisode.SearchTerms,
            PodcastName = legacyPodcast.Name,
            PodcastSearchTerms = legacyPodcast.SearchTerms,
            SearchLanguage = legacyEpisode.Language ?? legacyPodcast.Language,
            PodcastMetadataVersion = null
        };
    }
}
