using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Legacy;
using RedditPodcastPoster.Text.KnownTerms;
using System.Text.Json;
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

public sealed record PodcastParityVerificationResult(
    int SampledCount,
    int MatchingCount,
    IReadOnlyCollection<Guid> MissingInTargetIds,
    IReadOnlyCollection<Guid> MismatchedIds);

public sealed record EntityParityVerificationResult(
    string EntityName,
    int SampledCount,
    int MatchingCount,
    IReadOnlyCollection<string> MissingInTargetIds,
    IReadOnlyCollection<string> MismatchedIds);

public sealed record LegacyPodcastToV2MigrationSections(
    bool MigratePodcastsAndEpisodes = true,
    bool MigrateLookup = true,
    bool MigratePushSubscriptions = true,
    bool MigrateSubjects = true,
    bool MigrateDiscovery = true)
{
    public static LegacyPodcastToV2MigrationSections All { get; } = new();
    public static  LegacyPodcastToV2MigrationSections OnlyPodcastsAndEpisodes { get; } = new(MigrateLookup: false, MigratePushSubscriptions: false, MigrateSubjects: false, MigrateDiscovery: false);
}

public class LegacyPodcastToV2MigrationProcessor(
    IPodcastRepository legacyPodcastRepository,
    IPodcastRepositoryV2 podcastRepositoryV2,
    IEpisodeRepository episodeRepository,
    ILookupRepository lookupRepository,
    IEliminationTermsRepository eliminationTermsRepository,
    IKnownTermsRepository knownTermsRepository,
    IPushSubscriptionRepository legacyPushSubscriptionsRepository,
    IPushSubscriptionRepositoryV2 pushSubscriptionsRepository,
    ISubjectRepository legacySubjectsRepository,
    ISubjectRepositoryV2 subjectsRepository,
    IDiscoveryResultsRepository legacyDiscoveryRepository,
    IDiscoveryResultsRepositoryV2 discoveryRepository,
    [FromKeyedServices("v1")] Container legacyContainer,
    ILogger<LegacyPodcastToV2MigrationProcessor> logger)
{
    private static readonly TimeSpan ProgressUpdateInterval = TimeSpan.FromSeconds(5);

    public async Task<LegacyPodcastToV2MigrationResult> Run(
        LegacyPodcastToV2MigrationSections? sections = null,
        CancellationToken cancellationToken = default)
    {
        sections ??= LegacyPodcastToV2MigrationSections.All;

        var podcastsMigrated = 0;
        var episodesMigrated = 0;
        var failedPodcastIds = new List<Guid>();
        var failedEpisodeIds = new List<Guid>();

        var podcastProgressLastUpdate = DateTime.MinValue;
        var episodeProgressLastUpdate = DateTime.MinValue;
        var pushProgressLastUpdate = DateTime.MinValue;
        var subjectProgressLastUpdate = DateTime.MinValue;
        var discoveryProgressLastUpdate = DateTime.MinValue;
        var podcastEpisodesProgressLastUpdate = DateTime.MinValue;

        // Migrate Podcasts and Episodes
        var totalPodcasts = await legacyPodcastRepository.GetTotalCount();
        var totalEpisodes = await GetLegacyEpisodeCount(legacyContainer, cancellationToken);
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

                    if (legacyPodcast.Episodes.Count > 0)
                    {
                        var episodeBatch = legacyPodcast.Episodes.Select(e => ToV2Episode(legacyPodcast, e)).ToArray();
                        try
                        {
                            await episodeRepository.Save(episodeBatch);
                            migratedEpisodes += episodeBatch.Length;
                            episodesMigrated += episodeBatch.Length;
                        }
                        catch (Exception ex)
                        {
                            failedEpisodeIds.AddRange(episodeBatch.Select(x => x.Id));
                            logger.LogError(ex,
                                "Failed to migrate episodes for legacy podcast id '{PodcastId}' with count '{EpisodeCount}'.",
                                legacyPodcast.Id, episodeBatch.Length);
                            throw;
                        }
                    }

                    WriteProgress(
                        $"Migration progress: podcasts {podcastsMigrated}/{totalPodcasts} ({Percent(podcastsMigrated, totalPodcasts)}%), episodes {migratedEpisodes}/{totalEpisodes} ({Percent(migratedEpisodes, totalEpisodes)}%)",
                        ref podcastEpisodesProgressLastUpdate,
                        force: podcastsMigrated == totalPodcasts);
                }
                catch (Exception ex)
                {
                    failedPodcastIds.Add(legacyPodcast.Id);
                    failedEpisodeIds.AddRange(legacyPodcast.Episodes.Select(x => x.Id));
                    logger.LogError(ex,
                        "Failed to migrate legacy podcast id '{PodcastId}' and its episodes.",
                        legacyPodcast.Id);
                    throw;
                }
            }

            Console.WriteLine();
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
                throw;

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
                throw;

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
                WriteProgress(
                    $"PushSubscriptions migration progress: {i + 1}/{totalPush} ({Percent(i + 1, totalPush)}%)",
                    ref pushProgressLastUpdate,
                    force: i + 1 == totalPush);
            }
            Console.WriteLine();
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
                WriteProgress(
                    $"Subjects migration progress: {i + 1}/{totalSubjects} ({Percent(i + 1, totalSubjects)}%)",
                    ref subjectProgressLastUpdate,
                    force: i + 1 == totalSubjects);
            }
            Console.WriteLine();
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
                WriteProgress(
                    $"Discovery migration progress: {i + 1}/{totalDiscovery} ({Percent(i + 1, totalDiscovery)}%)",
                    ref discoveryProgressLastUpdate,
                    force: i + 1 == totalDiscovery);
            }
            Console.WriteLine();
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

    private static int Percent(int current, int total)
    {
        return current * 100 / (total == 0 ? 1 : total);
    }

    private static void WriteProgress(string message, ref DateTime lastUpdateUtc, bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && now - lastUpdateUtc < ProgressUpdateInterval)
        {
            return;
        }

        var width = Math.Max(Console.WindowWidth - 1, 20);
        if (message.Length > width)
        {
            message = message[..width];
        }

        Console.Write($"\r{message.PadRight(width)}");
        lastUpdateUtc = now;
    }

    public async Task<PodcastParityVerificationResult> VerifySampledPodcastParity(
        int sampleSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (sampleSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be greater than zero.");
        }

        var sampledIds = (await legacyPodcastRepository.GetAllIds().ToListAsync(cancellationToken))
            .Take(sampleSize)
            .ToList();

        var missingInTarget = new List<Guid>();
        var mismatched = new List<Guid>();
        var matchingCount = 0;

        foreach (var podcastId in sampledIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var legacyPodcast = await legacyPodcastRepository.GetPodcast(podcastId);
            if (legacyPodcast == null)
            {
                continue;
            }

            var targetPodcast = await podcastRepositoryV2.GetPodcast(podcastId);
            if (targetPodcast == null)
            {
                missingInTarget.Add(podcastId);
                continue;
            }

            var mismatchFields = GetPodcastFieldMismatches(legacyPodcast, targetPodcast);
            if (mismatchFields.Count == 0)
            {
                matchingCount++;
            }
            else
            {
                mismatched.Add(podcastId);
                logger.LogWarning(
                    "Podcast parity mismatch for podcast id '{PodcastId}'. Mismatched fields: {MismatchedFields}",
                    podcastId,
                    string.Join(", ", mismatchFields));
            }
        }

        return new PodcastParityVerificationResult(
            SampledCount: sampledIds.Count,
            MatchingCount: matchingCount,
            MissingInTargetIds: missingInTarget,
            MismatchedIds: mismatched);
    }

    public async Task<EntityParityVerificationResult> VerifySampledSubjectParity(
        int sampleSize = 25,
        CancellationToken cancellationToken = default)
    {
        var sampled = (await legacySubjectsRepository.GetAll().ToListAsync(cancellationToken))
            .Take(sampleSize)
            .ToList();

        var missing = new List<string>();
        var mismatched = new List<string>();
        var matching = 0;

        foreach (var legacySubject in sampled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = await subjectsRepository.GetBy(x => x.Id == legacySubject.Id);
            if (target == null)
            {
                missing.Add(legacySubject.Id.ToString());
                continue;
            }

            if (IsSubjectParityMatch(legacySubject, target))
            {
                matching++;
            }
            else
            {
                mismatched.Add(legacySubject.Id.ToString());
            }
        }

        return new EntityParityVerificationResult(
            EntityName: "Subjects",
            SampledCount: sampled.Count,
            MatchingCount: matching,
            MissingInTargetIds: missing,
            MismatchedIds: mismatched);
    }

    public async Task<EntityParityVerificationResult> VerifySampledDiscoveryParity(
        int sampleSize = 25,
        CancellationToken cancellationToken = default)
    {
        var sampled = (await legacyDiscoveryRepository.GetAll().ToListAsync(cancellationToken))
            .Take(sampleSize)
            .ToList();

        var missing = new List<string>();
        var mismatched = new List<string>();
        var matching = 0;

        foreach (var legacyDiscovery in sampled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = await discoveryRepository.GetById(legacyDiscovery.Id);
            if (target == null)
            {
                missing.Add(legacyDiscovery.Id.ToString());
                continue;
            }

            if (IsDiscoveryParityMatch(legacyDiscovery, target))
            {
                matching++;
            }
            else
            {
                mismatched.Add(legacyDiscovery.Id.ToString());
            }
        }

        return new EntityParityVerificationResult(
            EntityName: "Discovery",
            SampledCount: sampled.Count,
            MatchingCount: matching,
            MissingInTargetIds: missing,
            MismatchedIds: mismatched);
    }

    public async Task<EntityParityVerificationResult> VerifyLookupParity(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sampledCount = 0;
        var matching = 0;
        var missing = new List<string>();
        var mismatched = new List<string>();

        sampledCount++;
        var legacyElimination = await eliminationTermsRepository.Get();
        var targetElimination = await lookupRepository.GetEliminationTerms();
        if (targetElimination == null)
        {
            missing.Add("EliminationTerms");
        }
        else if (legacyElimination.Terms.SequenceEqual(targetElimination.Terms))
        {
            matching++;
        }
        else
        {
            mismatched.Add("EliminationTerms");
        }

        sampledCount++;
        var legacyKnownTerms = await knownTermsRepository.Get();
        var targetKnownTerms = await lookupRepository.GetKnownTerms<KnownTerms>();
        if (targetKnownTerms == null)
        {
            missing.Add("KnownTerms");
        }
        else if (IsKnownTermsParityMatch(legacyKnownTerms, targetKnownTerms))
        {
            matching++;
        }
        else
        {
            mismatched.Add("KnownTerms");
        }

        return new EntityParityVerificationResult(
            EntityName: "LookUps",
            SampledCount: sampledCount,
            MatchingCount: matching,
            MissingInTargetIds: missing,
            MismatchedIds: mismatched);
    }

    public async Task<EntityParityVerificationResult> VerifySampledPushSubscriptionParity(
        int sampleSize = 25,
        CancellationToken cancellationToken = default)
    {
        var sampled = (await legacyPushSubscriptionsRepository.GetAll().ToListAsync(cancellationToken))
            .Take(sampleSize)
            .ToList();
        var targetMap = (await pushSubscriptionsRepository.GetAll().ToListAsync(cancellationToken))
            .ToDictionary(x => x.Id, x => x);

        var missing = new List<string>();
        var mismatched = new List<string>();
        var matching = 0;

        foreach (var legacyPush in sampled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!targetMap.TryGetValue(legacyPush.Id, out var targetPush))
            {
                missing.Add(legacyPush.Id.ToString());
                continue;
            }

            if (IsPushSubscriptionParityMatch(legacyPush, targetPush))
            {
                matching++;
            }
            else
            {
                mismatched.Add(legacyPush.Id.ToString());
            }
        }

        return new EntityParityVerificationResult(
            EntityName: "PushSubscriptions",
            SampledCount: sampled.Count,
            MatchingCount: matching,
            MissingInTargetIds: missing,
            MismatchedIds: mismatched);
    }

    public async Task<EntityParityVerificationResult> VerifySampledEpisodeParity(
        int sampleSize = 25,
        CancellationToken cancellationToken = default)
    {
        var sampled = (await legacyPodcastRepository.GetAll().ToListAsync(cancellationToken))
            .SelectMany(podcast => podcast.Episodes.Select(episode => new { podcast, episode }))
            .Take(sampleSize)
            .ToList();

        var missing = new List<string>();
        var mismatched = new List<string>();
        var matching = 0;

        foreach (var sample in sampled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expected = ToV2Episode(sample.podcast, sample.episode);
            var target = await episodeRepository.GetEpisode(sample.podcast.Id, sample.episode.Id);
            if (target == null)
            {
                missing.Add(sample.episode.Id.ToString());
                continue;
            }

            var mismatchFields = GetEpisodeFieldMismatches(expected, target);
            if (mismatchFields.Count == 0)
            {
                matching++;
            }
            else
            {
                mismatched.Add(sample.episode.Id.ToString());
                logger.LogWarning(
                    "Episode parity mismatch for episode id '{EpisodeId}' (podcast-id '{PodcastId}'). Mismatched fields: {MismatchedFields}",
                    sample.episode.Id,
                    sample.podcast.Id,
                    string.Join(", ", mismatchFields));
            }
        }

        return new EntityParityVerificationResult(
            EntityName: "Episodes",
            SampledCount: sampled.Count,
            MatchingCount: matching,
            MissingInTargetIds: missing,
            MismatchedIds: mismatched);
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
            Removed = legacyPodcast.Removed,
            Publisher = legacyPodcast.Publisher,
            Bundles = legacyPodcast.Bundles,
            IndexAllEpisodes = legacyPodcast.IndexAllEpisodes,
            IgnoreAllEpisodes = legacyPodcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = legacyPodcast.BypassShortEpisodeChecking,
            MinimumDuration = legacyPodcast.MinimumDuration,
            ReleaseAuthority = legacyPodcast.ReleaseAuthority,
            PrimaryPostService = legacyPodcast.PrimaryPostService,
            SpotifyId = legacyPodcast.SpotifyId,
            SpotifyMarket = legacyPodcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = legacyPodcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = legacyPodcast.AppleId,
            YouTubeChannelId = legacyPodcast.YouTubeChannelId,
            YouTubePlaylistId = legacyPodcast.YouTubePlaylistId,
            YouTubePublicationOffset = legacyPodcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = legacyPodcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = legacyPodcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = legacyPodcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = legacyPodcast.TwitterHandle,
            BlueskyHandle = legacyPodcast.BlueskyHandle,
            HashTag = legacyPodcast.HashTag,
            EnrichmentHashTags = legacyPodcast.EnrichmentHashTags,
            TitleRegex = legacyPodcast.TitleRegex,
            DescriptionRegex = legacyPodcast.DescriptionRegex,
            EpisodeMatchRegex = legacyPodcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = legacyPodcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = legacyPodcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = legacyPodcast.IgnoredSubjects,
            DefaultSubject = legacyPodcast.DefaultSubject,
            SearchTerms = legacyPodcast.SearchTerms,
            KnownTerms = legacyPodcast.KnownTerms,
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
            PodcastLanguage = legacyPodcast.Language,
            Language = legacyEpisode.Language,
            PodcastMetadataVersion = null,
            PodcastRemoved = legacyPodcast.Removed,
            Images = legacyEpisode.Images,
            TwitterHandles = legacyEpisode.TwitterHandles,
            BlueskyHandles = legacyEpisode.BlueskyHandles
        };
    }

    private static bool IsPodcastFieldParityMatch(LegacyPodcast legacyPodcast, V2Podcast migratedPodcast)
    {
        return GetPodcastFieldMismatches(legacyPodcast, migratedPodcast).Count == 0;
    }

    private static IReadOnlyList<string> GetPodcastFieldMismatches(LegacyPodcast legacyPodcast, V2Podcast migratedPodcast)
    {
        var expectedPodcast = ToV2Podcast(legacyPodcast);
        using var expectedDocument = JsonSerializer.SerializeToDocument(expectedPodcast);
        using var actualDocument = JsonSerializer.SerializeToDocument(migratedPodcast);

        var mismatches = new List<string>();
        CollectJsonMismatches(expectedDocument.RootElement, actualDocument.RootElement, "$", mismatches);
        return mismatches;
    }

    private static void CollectJsonMismatches(JsonElement expected, JsonElement actual, string path, List<string> mismatches)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            mismatches.Add($"{path} (type: expected {expected.ValueKind}, actual {actual.ValueKind})");
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedProperties = expected.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
                var actualProperties = actual.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);

                foreach (var (propertyName, expectedValue) in expectedProperties)
                {
                    if (propertyName == "_ts")
                    {
                        continue;
                    }

                    var propertyPath = $"{path}.{propertyName}";
                    if (!actualProperties.TryGetValue(propertyName, out var actualValue))
                    {
                        mismatches.Add($"{propertyPath} (missing in target)");
                        continue;
                    }

                    CollectJsonMismatches(expectedValue, actualValue, propertyPath, mismatches);
                }

                foreach (var propertyName in actualProperties.Keys)
                {
                    if (propertyName == "_ts")
                    {
                        continue;
                    }

                    if (!expectedProperties.ContainsKey(propertyName))
                    {
                        mismatches.Add($"{path}.{propertyName} (unexpected in target)");
                    }
                }

                return;

            case JsonValueKind.Array:
                var expectedArray = expected.EnumerateArray().ToArray();
                var actualArray = actual.EnumerateArray().ToArray();

                if (expectedArray.Length != actualArray.Length)
                {
                    mismatches.Add($"{path} (length: expected {expectedArray.Length}, actual {actualArray.Length})");
                    return;
                }

                for (var i = 0; i < expectedArray.Length; i++)
                {
                    CollectJsonMismatches(expectedArray[i], actualArray[i], $"{path}[{i}]", mismatches);
                }

                return;

            default:
                if (expected.GetRawText() != actual.GetRawText())
                {
                    mismatches.Add($"{path} (expected {expected.GetRawText()}, actual {actual.GetRawText()})");
                }

                return;
        }
    }

    private static bool IsSubjectParityMatch(RedditPodcastPoster.Models.Subject legacy, RedditPodcastPoster.Models.Subject target)
    {
        return legacy.Id == target.Id &&
               legacy.Name == target.Name &&
               legacy.SubjectType == target.SubjectType &&
               SequenceEqual(legacy.Aliases, target.Aliases) &&
               SequenceEqual(legacy.AssociatedSubjects, target.AssociatedSubjects) &&
               legacy.RedditFlairTemplateId == target.RedditFlairTemplateId &&
               legacy.RedditFlareText == target.RedditFlareText &&
               legacy.HashTag == target.HashTag &&
               SequenceEqual(legacy.EnrichmentHashTags, target.EnrichmentHashTags) &&
               SequenceEqual(legacy.KnownTerms, target.KnownTerms);
    }

    private static bool IsDiscoveryParityMatch(RedditPodcastPoster.Models.DiscoveryResultsDocument legacy, RedditPodcastPoster.Models.DiscoveryResultsDocument target)
    {
        return legacy.Id == target.Id &&
               legacy.State == target.State &&
               legacy.DiscoveryBegan == target.DiscoveryBegan &&
               legacy.ExcludeSpotify == target.ExcludeSpotify &&
               legacy.IncludeYouTube == target.IncludeYouTube &&
               legacy.IncludeListenNotes == target.IncludeListenNotes &&
               legacy.IncludeTaddy == target.IncludeTaddy &&
               legacy.EnrichListenNotesFromSpotify == target.EnrichListenNotesFromSpotify &&
               legacy.EnrichFromSpotify == target.EnrichFromSpotify &&
               legacy.EnrichFromApple == target.EnrichFromApple &&
               legacy.SearchSince == target.SearchSince &&
               legacy.PreSkipSpotifyUrlResolving == target.PreSkipSpotifyUrlResolving &&
               legacy.PostSkipSpotifyUrlResolving == target.PostSkipSpotifyUrlResolving &&
               AreDiscoveryResultsEqual(legacy.DiscoveryResults, target.DiscoveryResults);
    }

    private static bool AreDiscoveryResultsEqual(
        IEnumerable<RedditPodcastPoster.Models.DiscoveryResult> left,
        IEnumerable<RedditPodcastPoster.Models.DiscoveryResult> right)
    {
        var leftList = left.ToList();
        var rightList = right.ToList();

        if (leftList.Count != rightList.Count)
        {
            return false;
        }

        for (var i = 0; i < leftList.Count; i++)
        {
            if (!IsDiscoveryResultEqual(leftList[i], rightList[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDiscoveryResultEqual(
        RedditPodcastPoster.Models.DiscoveryResult left,
        RedditPodcastPoster.Models.DiscoveryResult right)
    {
        return left.Id == right.Id &&
               left.EpisodeName == right.EpisodeName &&
               left.ShowName == right.ShowName &&
               left.Released == right.Released &&
               left.Length == right.Length &&
               left.ShowDescription == right.ShowDescription &&
               left.Description == right.Description &&
               left.State == right.State &&
               AreDiscoveryResultUrlsEqual(left.Urls, right.Urls) &&
               SequenceEqual(left.Subjects, right.Subjects) &&
               left.YouTubeViews == right.YouTubeViews &&
               left.YouTubeChannelMembers == right.YouTubeChannelMembers &&
               left.ImageUrl == right.ImageUrl &&
               SequenceEqual(left.Sources, right.Sources) &&
               left.EnrichedTimeFromApple == right.EnrichedTimeFromApple &&
               left.EnrichedUrlFromSpotify == right.EnrichedUrlFromSpotify &&
               SequenceEqual(left.MatchingPodcastIds, right.MatchingPodcastIds);
    }

    private static bool AreDiscoveryResultUrlsEqual(
        RedditPodcastPoster.Models.DiscoveryResultUrls left,
        RedditPodcastPoster.Models.DiscoveryResultUrls right)
    {
        return left.Spotify == right.Spotify &&
               left.Apple == right.Apple &&
               left.YouTube == right.YouTube;
    }

    private static bool IsKnownTermsParityMatch(KnownTerms legacy, KnownTerms target)
    {
        if (legacy.Terms.Count != target.Terms.Count)
        {
            return false;
        }

        foreach (var (key, value) in legacy.Terms)
        {
            if (!target.Terms.TryGetValue(key, out var targetValue))
            {
                return false;
            }

            if (value.ToString() != targetValue.ToString() || value.Options != targetValue.Options)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPushSubscriptionParityMatch(RedditPodcastPoster.Models.PushSubscription legacy, RedditPodcastPoster.Models.PushSubscription target)
    {
        return legacy.Id == target.Id &&
               legacy.User == target.User &&
               legacy.Endpoint == target.Endpoint &&
               legacy.ExpirationTime == target.ExpirationTime &&
               legacy.Auth == target.Auth &&
               legacy.P256Dh == target.P256Dh;
    }

    private static IReadOnlyList<string> GetEpisodeFieldMismatches(V2Episode expected, V2Episode target)
    {
        var mismatches = new List<string>();

        if (expected.Id != target.Id) mismatches.Add(nameof(V2Episode.Id));
        if (expected.PodcastId != target.PodcastId) mismatches.Add(nameof(V2Episode.PodcastId));
        if (expected.Title != target.Title) mismatches.Add(nameof(V2Episode.Title));
        if (expected.Description != target.Description) mismatches.Add(nameof(V2Episode.Description));
        if (expected.Release != target.Release) mismatches.Add(nameof(V2Episode.Release));
        if (expected.Length != target.Length) mismatches.Add(nameof(V2Episode.Length));
        if (expected.Explicit != target.Explicit) mismatches.Add(nameof(V2Episode.Explicit));
        if (expected.Posted != target.Posted) mismatches.Add(nameof(V2Episode.Posted));
        if (expected.Tweeted != target.Tweeted) mismatches.Add(nameof(V2Episode.Tweeted));
        if (expected.BlueskyPosted != target.BlueskyPosted) mismatches.Add(nameof(V2Episode.BlueskyPosted));
        if (expected.Ignored != target.Ignored) mismatches.Add(nameof(V2Episode.Ignored));
        if (expected.Removed != target.Removed) mismatches.Add(nameof(V2Episode.Removed));
        if (expected.SpotifyId != target.SpotifyId) mismatches.Add(nameof(V2Episode.SpotifyId));
        if (expected.AppleId != target.AppleId) mismatches.Add(nameof(V2Episode.AppleId));
        if (expected.YouTubeId != target.YouTubeId) mismatches.Add(nameof(V2Episode.YouTubeId));
        if (!AreServiceUrlsEqual(expected.Urls, target.Urls)) mismatches.Add(nameof(V2Episode.Urls));
        if (!SequenceEqual(expected.Subjects, target.Subjects)) mismatches.Add(nameof(V2Episode.Subjects));
        if (expected.SearchTerms != target.SearchTerms) mismatches.Add(nameof(V2Episode.SearchTerms));
        if (expected.PodcastName != target.PodcastName) mismatches.Add(nameof(V2Episode.PodcastName));
        if (expected.PodcastSearchTerms != target.PodcastSearchTerms) mismatches.Add(nameof(V2Episode.PodcastSearchTerms));
        if (expected.PodcastLanguage != target.PodcastLanguage) mismatches.Add(nameof(V2Episode.PodcastLanguage));
        if (expected.Language != target.Language) mismatches.Add(nameof(V2Episode.Language));
        if (expected.PodcastMetadataVersion != target.PodcastMetadataVersion) mismatches.Add(nameof(V2Episode.PodcastMetadataVersion));
        if (expected.PodcastRemoved != target.PodcastRemoved) mismatches.Add(nameof(V2Episode.PodcastRemoved));
        if (!AreEpisodeImagesEqual(expected.Images, target.Images)) mismatches.Add(nameof(V2Episode.Images));
        if (!SequenceEqual(expected.TwitterHandles, target.TwitterHandles)) mismatches.Add(nameof(V2Episode.TwitterHandles));
        if (!SequenceEqual(expected.BlueskyHandles, target.BlueskyHandles)) mismatches.Add(nameof(V2Episode.BlueskyHandles));

        return mismatches;
    }

    private static bool IsEpisodeParityMatch(V2Episode expected, V2Episode target)
    {
        return GetEpisodeFieldMismatches(expected, target).Count == 0;
    }

    private static bool AreServiceUrlsEqual(RedditPodcastPoster.Models.ServiceUrls? left, RedditPodcastPoster.Models.ServiceUrls? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.Spotify == right.Spotify &&
               left.Apple == right.Apple &&
               left.YouTube == right.YouTube &&
               left.InternetArchive == right.InternetArchive &&
               left.BBC == right.BBC;
    }

    private static bool AreEpisodeImagesEqual(RedditPodcastPoster.Models.EpisodeImages? left, RedditPodcastPoster.Models.EpisodeImages? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.YouTube == right.YouTube &&
               left.Spotify == right.Spotify &&
               left.Apple == right.Apple &&
               left.Other == right.Other;
    }

    private static bool SequenceEqual(string[]? left, string[]? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.SequenceEqual(right);
    }

    private static bool SequenceEqual<T>(IEnumerable<T>? left, IEnumerable<T>? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.SequenceEqual(right);
    }
}
