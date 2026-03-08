using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.KnownTerms;
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

public sealed record LegacyPodcastToV2MigrationSections(
    bool MigratePodcastsAndEpisodes = true,
    bool MigrateLookup = true,
    bool MigratePushSubscriptions = true,
    bool MigrateSubjects = true,
    bool MigrateDiscovery = true)
{
    public static LegacyPodcastToV2MigrationSections All { get; } = new();
}

public class LegacyPodcastToV2MigrationProcessor(
    IPodcastRepository legacyPodcastRepository,
    IPodcastRepositoryV2 podcastRepositoryV2,
    IEpisodeRepository episodeRepository,
    ILookupRepositoryV2 lookupRepository,
    IEliminationTermsRepository eliminationTermsRepository,
    IKnownTermsRepository knownTermsRepository,
    IPushSubscriptionRepository legacyPushSubscriptionsRepository,
    IPushSubscriptionRepositoryV2 pushSubscriptionsRepository,
    ISubjectRepository legacySubjectsRepository,
    ISubjectRepositoryV2 subjectsRepository,
    IDiscoveryResultsRepository legacyDiscoveryRepository,
    IDiscoveryResultsRepositoryV2 discoveryRepository,
    ICosmosDbContainerFactory legacyContainerFactory,
    ILogger<LegacyPodcastToV2MigrationProcessor> logger)
{
    public async Task<LegacyPodcastToV2MigrationResult> Run(
        LegacyPodcastToV2MigrationSections? sections = null,
        CancellationToken cancellationToken = default)
    {
        sections ??= LegacyPodcastToV2MigrationSections.All;

        var podcastsMigrated = 0;
        var episodesMigrated = 0;
        var failedPodcastIds = new List<Guid>();
        var failedEpisodeIds = new List<Guid>();

        // Migrate Podcasts and Episodes
        var totalPodcasts = await legacyPodcastRepository.GetTotalCount();
        var totalEpisodes = await GetLegacyEpisodeCount(legacyContainerFactory.Create(), cancellationToken);
        if (sections.MigratePodcastsAndEpisodes)
        {
            var legacyPodcasts = await legacyPodcastRepository.GetAll().ToListAsync(cancellationToken);
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
        }
        else
        {
            Console.WriteLine("Skipping Podcasts and Episodes migration.");
        }

        if (sections.MigrateLookup)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var eliminationTerms = await eliminationTermsRepository.Get();
                await lookupRepository.SaveEliminationTerms(eliminationTerms);
                Console.WriteLine("Lookup migration progress: EliminationTerms migrated.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to migrate EliminationTerms into v2 LookUps repository.");
            }

            try
            {
                var knownTerms = await knownTermsRepository.Get();
                await lookupRepository.SaveKnownTerms(knownTerms);
                Console.WriteLine("Lookup migration progress: KnownTerms migrated.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to migrate KnownTerms into v2 LookUps repository.");
            }
        }
        else
        {
            Console.WriteLine("Skipping Lookup migration.");
        }

        if (sections.MigratePushSubscriptions)
        {
            // Migrate PushSubscriptions
            var pushSubscriptions = await legacyPushSubscriptionsRepository.GetAll().ToListAsync(cancellationToken);
            var totalPush = pushSubscriptions.Count;
            for (int i = 0; i < totalPush; i++)
            {
                await pushSubscriptionsRepository.Save(pushSubscriptions[i]);
                Console.WriteLine(
                    $"PushSubscriptions migration progress: {i + 1}/{totalPush} ({(i + 1) * 100 / (totalPush == 0 ? 1 : totalPush)}%)");
            }
        }
        else
        {
            Console.WriteLine("Skipping PushSubscriptions migration.");
        }

        if (sections.MigrateSubjects)
        {
            // Migrate Subjects
            var subjects = await legacySubjectsRepository.GetAll().ToListAsync(cancellationToken);
            var totalSubjects = subjects.Count;
            for (int i = 0; i < totalSubjects; i++)
            {
                await subjectsRepository.Save(subjects[i]);
                Console.WriteLine(
                    $"Subjects migration progress: {i + 1}/{totalSubjects} ({(i + 1) * 100 / (totalSubjects == 0 ? 1 : totalSubjects)}%)");
            }
        }
        else
        {
            Console.WriteLine("Skipping Subjects migration.");
        }

        if (sections.MigrateDiscovery)
        {
            // Migrate Discovery (all legacy documents)
            var discoveries = await legacyDiscoveryRepository.GetAll().ToListAsync(cancellationToken);
            var totalDiscovery = discoveries.Count;
            for (int i = 0; i < totalDiscovery; i++)
            {
                await discoveryRepository.Save(discoveries[i]);
                Console.WriteLine(
                    $"Discovery migration progress: {i + 1}/{totalDiscovery} ({(i + 1) * 100 / (totalDiscovery == 0 ? 1 : totalDiscovery)}%)");
            }
        }
        else
        {
            Console.WriteLine("Skipping Discovery migration.");
        }

        return new LegacyPodcastToV2MigrationResult(
            PodcastsMigrated: podcastsMigrated,
            EpisodesMigrated: episodesMigrated,
            FailedPodcastIds: failedPodcastIds,
            FailedEpisodeIds: failedEpisodeIds);
    }

    private static async Task<int> GetLegacyEpisodeCount(Container legacyContainer, CancellationToken cancellationToken)
    {
        const string queryText = "SELECT VALUE SUM(ARRAY_LENGTH(c.episodes)) FROM c WHERE c.type = 'Podcast'";
        var iterator = legacyContainer.GetItemQueryIterator<int?>(new QueryDefinition(queryText));

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var count = response.FirstOrDefault();
            return count ?? 0;
        }

        return 0;
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
