using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;

namespace FindDuplicateEpisodes;

public class FindDuplicateEpisodesProcessor(
    IOptions<CosmosDbSettings> cosmosDbSettings,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    ILogger<FindDuplicateEpisodesProcessor> logger)
{
    private const string ActiveEpisodesFilter =
        "((NOT IS_DEFINED(e.podcastRemoved)) OR e.podcastRemoved=false) and ((NOT IS_DEFINED(e.removed)) OR e.removed=false)";

    private static readonly HashSet<string> ExcludedComparisonFields =
        new(StringComparer.Ordinal) { "id", "_rid", "_self", "_etag", "_attachments", "_ts", "posted", "tweeted", "bluesky", "description" };

    private readonly CosmosDbSettings _cosmosDbSettings = cosmosDbSettings.Value;
    private readonly JsonSerializerOptions _jsonSerializerOptions = jsonSerializerOptionsProvider.GetJsonSerializerOptions();

    public async Task Run(FindDuplicateEpisodesRequest request)
    {
        logger.LogWarning(
            request.VerifyDeduplication ? "Running in VERIFY mode — checking canonical ignored/removed state using backed-up deleted episodes." :
            request.NotDryRun ? "Running in LIVE mode — all changes will be applied." :
            request.DeleteNoDiff ? "Running in DELETE-NO-DIFF mode — only pure duplicates will be deleted (with file backup)." :
            "Running in DRY-RUN mode — no changes will be made.");

        using var cosmosClient = CreateCosmosClient();
        var container = cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.EpisodesContainer);

        if (request.VerifyDeduplication)
        {
            await VerifyDeduplication(container);
            return;
        }

        var query = $@"SELECT e.id, e.podcastId, e.title, e.release, e.spotifyId, e.appleId, e.youTubeId, e.podcastName
                       FROM episodes e
                       WHERE {ActiveEpisodesFilter}";
        var iterator = container.GetItemQueryIterator<EpisodeDuplicateSample>(new QueryDefinition(query));
        var seen = new Dictionary<string, EpisodeDuplicateSample>(StringComparer.Ordinal);
        var duplicatePairs = new List<(EpisodeDuplicateSample First, EpisodeDuplicateSample Second)>();

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            foreach (var item in page)
            {
                var fingerprint = CreateDuplicateFingerprint(item);
                if (string.IsNullOrWhiteSpace(fingerprint))
                    continue;

                if (seen.TryGetValue(fingerprint, out var existing) && existing.Id != item.Id)
                {
                    if (!string.IsNullOrWhiteSpace(existing.PodcastName) &&
                        !string.IsNullOrWhiteSpace(item.PodcastName) &&
                        !string.Equals(existing.PodcastName.Trim(), item.PodcastName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        //logger.LogInformation(
                        //    "Shared service-ID (Fingerprint={Fingerprint}) across different podcasts '{FirstPodcastName}' / '{SecondPodcastName}' — treated as distinct episodes.",
                        //    fingerprint, existing.PodcastName, item.PodcastName);
                    }
                    else
                    {
                        //logger.LogWarning(
                        //    "Potential duplicate pair: Fingerprint={Fingerprint}; First={FirstId}/{FirstPodcastId}/{FirstPodcastName}/{FirstTitle}/{FirstRelease}; Second={SecondId}/{SecondPodcastId}/{SecondPodcastName}/{SecondTitle}/{SecondRelease}",
                        //    fingerprint,
                        //    existing.Id, existing.PodcastId, existing.PodcastName ?? string.Empty, existing.Title, existing.Release,
                        //    item.Id, item.PodcastId, item.PodcastName ?? string.Empty, item.Title, item.Release);
                        duplicatePairs.Add((existing, item));
                    }
                }

                seen[fingerprint] = item;
            }
        }

        if (duplicatePairs.Count == 0)
        {
            logger.LogInformation("No duplicate episode pairs found.");
            return;
        }

        logger.LogWarning("Found {Count} duplicate pair(s). Processing...", duplicatePairs.Count);

        if (request.NotDryRun || request.DeleteNoDiff)
        {
            Directory.CreateDirectory(BackupFolder);
        }

        var reports = new List<DedupPairReport>();
        foreach (var (first, second) in duplicatePairs)
        {
            var report = await ProcessDuplicatePair(container, first, second, request);
            if (report != null)
                reports.Add(report);
        }

        if (reports.Count > 0)
        {
            logger.LogWarning("=== Deduplication report: {Count} pair(s) ===", reports.Count);
            foreach (var r in reports)
            {
                logger.LogWarning(
                    "Canonical: {KeeperId} '{KeeperTitle}' [{KeeperPodcastName}] wasPublished={WasPublished}; Duplicate: {ToDeleteId} '{ToDeleteTitle}'; Outcome: {Outcome}; Differences: {Differences}",
                    r.KeeperId, r.KeeperTitle, r.KeeperPodcastName ?? string.Empty, r.KeeperWasPublished,
                    r.ToDeleteId, r.ToDeleteTitle, r.Outcome,
                    r.Differences.Count > 0 ? string.Join("; ", r.Differences) : "(none)");
            }
        }
    }

    private async Task<DedupPairReport?> ProcessDuplicatePair(
        Container container,
        EpisodeDuplicateSample firstSample,
        EpisodeDuplicateSample secondSample,
        FindDuplicateEpisodesRequest request)
    {
        if (!Guid.TryParse(firstSample.PodcastId, out var firstPodcastId) ||
            !Guid.TryParse(firstSample.Id, out var firstEpisodeId))
        {
            logger.LogError("Cannot parse IDs for first sample: PodcastId={PodcastId}, Id={Id}.",
                firstSample.PodcastId, firstSample.Id);
            return null;
        }

        if (!Guid.TryParse(secondSample.PodcastId, out var secondPodcastId) ||
            !Guid.TryParse(secondSample.Id, out var secondEpisodeId))
        {
            logger.LogError("Cannot parse IDs for second sample: PodcastId={PodcastId}, Id={Id}.",
                secondSample.PodcastId, secondSample.Id);
            return null;
        }

        var firstEpisode = await episodeRepository.GetEpisode(firstPodcastId, firstEpisodeId);
        var secondEpisode = await episodeRepository.GetEpisode(secondPodcastId, secondEpisodeId);

        if (firstEpisode == null || secondEpisode == null)
        {
            logger.LogWarning("Pair {FirstId}/{SecondId}: could not load one or both episodes.",
                firstSample.Id, secondSample.Id);
            return null;
        }

        var firstPodcast = await podcastRepository.GetPodcast(firstPodcastId);
        var secondPodcast = firstPodcastId == secondPodcastId
            ? firstPodcast
            : await podcastRepository.GetPodcast(secondPodcastId);

        // Canonical = has been publicly posted/tweeted/bluesky'd
        var firstIsCanonical = firstEpisode.Posted || firstEpisode.Tweeted || firstEpisode.BlueskyPosted == true;
        var secondIsCanonical = secondEpisode.Posted || secondEpisode.Tweeted || secondEpisode.BlueskyPosted == true;

        // Override: when episodes span different podcasts, the episode under a removed podcast
        // must always be the duplicate — podcast.Removed is the authoritative flag.
        if (firstPodcastId != secondPodcastId)
        {
            var firstPodcastRemoved = firstPodcast?.Removed == true;
            var secondPodcastRemoved = secondPodcast?.Removed == true;

            if (firstPodcastRemoved && !secondPodcastRemoved)
            {
                logger.LogWarning(
                    "Pair {FirstId}/{SecondId}: cross-podcast duplicate — podcast {FirstPodcastId} is removed; overriding canonical to {SecondId}.",
                    firstEpisode.Id, secondEpisode.Id, firstPodcastId, secondEpisode.Id);
                firstIsCanonical = false;
                secondIsCanonical = true;
            }
            else if (secondPodcastRemoved && !firstPodcastRemoved)
            {
                logger.LogWarning(
                    "Pair {FirstId}/{SecondId}: cross-podcast duplicate — podcast {SecondPodcastId} is removed; overriding canonical to {FirstId}.",
                    firstEpisode.Id, secondEpisode.Id, secondPodcastId, firstEpisode.Id);
                firstIsCanonical = true;
                secondIsCanonical = false;
            }
            else
            {
                logger.LogWarning(
                    "Pair {FirstId}/{SecondId}: cross-podcast duplicate (podcasts {FirstPodcastId} / {SecondPodcastId}) — neither or both podcasts removed; falling through to posted-flag check.",
                    firstEpisode.Id, secondEpisode.Id, firstPodcastId, secondPodcastId);
            }
        }

        if (firstIsCanonical && secondIsCanonical)
        {
            logger.LogWarning(
                "Pair {FirstId}/{SecondId}: both episodes are marked posted/tweeted/bluesky. Ignoring these flags for dedupe tie-break.",
                firstEpisode.Id, secondEpisode.Id);
            firstIsCanonical = false;
            secondIsCanonical = false;
        }

        Episode keeper;
        Episode toDelete;
        Podcast? keeperPodcast;
        bool keeperIsPublished;

        if (firstIsCanonical)
        {
            keeper = firstEpisode;
            keeperPodcast = firstPodcast;
            toDelete = secondEpisode;
            keeperIsPublished = true;
            logger.LogInformation("Pair {FirstId}/{SecondId}: keeping {KeeperId} (canonical — published).",
                firstEpisode.Id, secondEpisode.Id, keeper.Id);
        }
        else if (secondIsCanonical)
        {
            keeper = secondEpisode;
            keeperPodcast = secondPodcast;
            toDelete = firstEpisode;
            keeperIsPublished = true;
            logger.LogInformation("Pair {FirstId}/{SecondId}: keeping {KeeperId} (canonical — published).",
                firstEpisode.Id, secondEpisode.Id, keeper.Id);
        }
        else
        {
            // Neither published — keep the first by encounter order
            keeper = firstEpisode;
            keeperPodcast = firstPodcast;
            toDelete = secondEpisode;
            keeperIsPublished = false;
            logger.LogWarning(
                "Pair {FirstId}/{SecondId}: neither canonical — keeping {KeeperId} by encounter order.",
                firstEpisode.Id, secondEpisode.Id, keeper.Id);
        }

        var keeperDoc = await FetchEpisodeAsDocument(container, keeper.Id.ToString());
        var toDeleteDoc = await FetchEpisodeAsDocument(container, toDelete.Id.ToString());
        var hasDifferences = HasMeaningfulDifferences(keeperDoc, toDeleteDoc, out var differences);

        var updated = false;
        var urlsMerged = MergeUrls(keeper, toDelete);
        if (urlsMerged)
        {
            logger.LogWarning("Pair {KeeperId}/{ToDeleteId}: merged missing URLs from duplicate into keeper.",
                keeper.Id, toDelete.Id);
            updated = true;
        }

        if (toDelete.Description.Length > keeper.Description.Length)
        {
            logger.LogWarning(
                "Pair {KeeperId}/{ToDeleteId}: taking longer description from duplicate ({DuplicateLength} chars > {KeeperLength} chars).",
                keeper.Id, toDelete.Id, toDelete.Description.Length, keeper.Description.Length);
            keeper.Description = toDelete.Description;
            updated = true;
        }

        if (keeper.Ignored && !toDelete.Ignored)
        {
            logger.LogWarning(
                "Pair {KeeperId}/{ToDeleteId}: resetting keeper.Ignored from true to false based on duplicate.",
                keeper.Id, toDelete.Id);
            keeper.Ignored = false;
            updated = true;
        }

        if (keeper.Removed && !toDelete.Removed)
        {
            logger.LogWarning(
                "Pair {KeeperId}/{ToDeleteId}: resetting keeper.Removed from true to false based on duplicate.",
                keeper.Id, toDelete.Id);
            keeper.Removed = false;
            updated = true;
        }

        if (!hasDifferences)
        {
            logger.LogWarning(
                "Pair {KeeperId}/{ToDeleteId}: no meaningful differences — will delete {ToDeleteId}.",
                keeper.Id, toDelete.Id, toDelete.Id);
            if (request.NotDryRun || request.DeleteNoDiff)
            {
                await BackupEpisodeToFile(toDelete);
                if (updated)
                {
                    await episodeRepository.Save(keeper);
                    logger.LogWarning("Updated keeper {KeeperId} with merged URLs.", keeper.Id);
                }

                await episodeRepository.Delete(toDelete.PodcastId, toDelete.Id);
                logger.LogWarning("Deleted {ToDeleteId}.", toDelete.Id);
            }

            return new DedupPairReport(keeper.Id, keeper.Title, keeper.PodcastName,
                keeperIsPublished, toDelete.Id, toDelete.Title, [],
                (request.NotDryRun || request.DeleteNoDiff) ? (urlsMerged ? "DeletedWithUrlMerge" : "Deleted") : "DryRunWouldDelete");
        }

        logger.LogWarning(
            "Pair {KeeperId}/{ToDeleteId} has {Count} difference(s): {Differences}",
            keeper.Id, toDelete.Id, differences.Count, string.Join("; ", differences));

        // Release date: use the earliest of the two (most likely correct)
        var earliestRelease = firstEpisode.Release <= secondEpisode.Release
            ? firstEpisode.Release
            : secondEpisode.Release;
        if (keeper.Release != earliestRelease)
        {
            logger.LogWarning("Pair {KeeperId}: correcting release {Old} -> {New} (earliest of pair).",
                keeper.Id, keeper.Release, earliestRelease);
            keeper.Release = earliestRelease;
            updated = true;
        }

        // Duration: use the episode whose primary-post-service ID is present
        var correctDuration = GetCorrectDuration(firstEpisode, secondEpisode, keeperPodcast);
        if (correctDuration.HasValue && keeper.Length != correctDuration.Value)
        {
            logger.LogWarning("Pair {KeeperId}: correcting duration {Old} -> {New} (primary-post-service).",
                keeper.Id, keeper.Length, correctDuration.Value);
            keeper.Length = correctDuration.Value;
            updated = true;
        }

        logger.LogWarning(
            "Pair {KeeperId}/{ToDeleteId}: will {Action} keeper and delete {ToDeleteId}.",
            keeper.Id, toDelete.Id,
            updated ? "update then keep" : "keep unchanged",
            toDelete.Id);

        if (request.NotDryRun)
        {
            await BackupEpisodeToFile(toDelete);
            if (updated)
            {
                await episodeRepository.Save(keeper);
                logger.LogWarning("Updated keeper {KeeperId}.", keeper.Id);
            }

            await episodeRepository.Delete(toDelete.PodcastId, toDelete.Id);
            logger.LogWarning("Deleted duplicate {ToDeleteId}.", toDelete.Id);
        }

        return new DedupPairReport(keeper.Id, keeper.Title, keeper.PodcastName,
            keeperIsPublished, toDelete.Id, toDelete.Title, differences.AsReadOnly(),
            request.NotDryRun
                ? (updated ? "UpdatedAndDeleted" : "Deleted")
                : (updated ? "DryRunWouldUpdateAndDelete" : "DryRunWouldDelete"));
    }

    private const string BackupFolder = "dedupe-episodes";
    private const string LegacyBackupFolder = "deduped-episodes";

    private async Task BackupEpisodeToFile(Episode episode)
    {
        var filePath = Path.Combine(BackupFolder, $"{episode.Id}.json");
        var json = JsonSerializer.Serialize(episode, _jsonSerializerOptions);
        await File.WriteAllTextAsync(filePath, json);
        logger.LogInformation("Backed up episode {Id} to '{FilePath}'.", episode.Id, filePath);
    }

    private async Task VerifyDeduplication(Container container)
    {
        var deletedEpisodes = await LoadDeletedEpisodesFromBackups();
        if (deletedEpisodes.Count == 0)
        {
            logger.LogWarning(
                "No backup episode files found in '{BackupFolder}' or '{LegacyBackupFolder}'. Nothing to verify.",
                BackupFolder,
                LegacyBackupFolder);
            return;
        }

        var groups = deletedEpisodes
            .GroupBy(x => $"{(x.PodcastName ?? string.Empty).Trim().ToUpperInvariant()}|{x.Title.Trim().ToUpperInvariant()}")
            .ToList();

        var checkedCanonicals = 0;
        var violations = 0;

        foreach (var group in groups)
        {
            var representative = group.First();
            var podcastName = representative.PodcastName ?? string.Empty;
            var title = representative.Title;

            if (string.IsNullOrWhiteSpace(podcastName) || string.IsNullOrWhiteSpace(title))
            {
                logger.LogWarning(
                    "Skipping backup group with missing podcastName/title. PodcastName='{PodcastName}', Title='{Title}'.",
                    podcastName,
                    title);
                continue;
            }

            var deletedById = group.ToDictionary(x => x.Id, x => x);
            var canonicals = await QueryCanonicalCandidates(container, podcastName, title);
            canonicals = canonicals
                .Where(x => !deletedById.ContainsKey(x.Id))
                .ToList();

            if (canonicals.Count == 0)
            {
                logger.LogWarning(
                    "No canonical found for backup group Podcast='{PodcastName}', Title='{Title}'.",
                    podcastName,
                    title);
                continue;
            }

            if (canonicals.Count > 1)
            {
                logger.LogWarning(
                    "Multiple canonical candidates ({Count}) found for Podcast='{PodcastName}', Title='{Title}'. Verifying each.",
                    canonicals.Count,
                    podcastName,
                    title);
            }

            var anyDeletedNotIgnored = group.Any(x => !x.Ignored);
            var anyDeletedNotRemoved = group.Any(x => !x.Removed);

            foreach (var canonical in canonicals)
            {
                checkedCanonicals++;
                var canonicalInvalidByIgnored = canonical.Ignored && anyDeletedNotIgnored;
                var canonicalInvalidByRemoved = canonical.Removed && anyDeletedNotRemoved;

                if (canonicalInvalidByIgnored || canonicalInvalidByRemoved)
                {
                    violations++;
                    logger.LogError(
                        "VERIFY FAILED: Canonical {CanonicalId} ('{PodcastName}' / '{Title}') has Ignored={Ignored}, Removed={Removed} but deleted duplicates include Ignored=false or Removed=false.",
                        canonical.Id,
                        canonical.PodcastName ?? string.Empty,
                        canonical.Title,
                        canonical.Ignored,
                        canonical.Removed);
                }
                else
                {
                    logger.LogInformation(
                        "VERIFY OK: Canonical {CanonicalId} ('{PodcastName}' / '{Title}') has Ignored={Ignored}, Removed={Removed} consistent with deleted duplicates.",
                        canonical.Id,
                        canonical.PodcastName ?? string.Empty,
                        canonical.Title,
                        canonical.Ignored,
                        canonical.Removed);
                }
            }
        }

        logger.LogWarning(
            "Verification summary: DeletedBackups={DeletedCount}; Groups={GroupCount}; CanonicalsChecked={Checked}; Violations={Violations}",
            deletedEpisodes.Count,
            groups.Count,
            checkedCanonicals,
            violations);
    }

    private async Task<List<Episode>> LoadDeletedEpisodesFromBackups()
    {
        var episodes = new List<Episode>();
        var folders = new[] { BackupFolder, LegacyBackupFolder }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .ToList();

        foreach (var folder in folders)
        {
            foreach (var filePath in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var episode = JsonSerializer.Deserialize<Episode>(json, _jsonSerializerOptions);
                    if (episode == null)
                    {
                        logger.LogWarning("Unable to deserialize backup file '{FilePath}'.", filePath);
                        continue;
                    }

                    episodes.Add(episode);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed reading backup file '{FilePath}'.", filePath);
                }
            }
        }

        return episodes;
    }

    private static async Task<List<CanonicalEpisodeSample>> QueryCanonicalCandidates(
        Container container,
        string podcastName,
        string title)
    {
        var query = new QueryDefinition(
                "SELECT e.id, e.title, e.podcastName, e.ignored, e.removed FROM episodes e WHERE e.podcastName = @podcastName AND e.title = @title")
            .WithParameter("@podcastName", podcastName)
            .WithParameter("@title", title);

        var iterator = container.GetItemQueryIterator<CanonicalEpisodeSample>(query);
        var items = new List<CanonicalEpisodeSample>();

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            items.AddRange(page);
        }

        return items;
    }

    // Copies any non-null URL from source into target where target has no value.
    // Returns true if any URL was merged.
    private static bool MergeUrls(Episode target, Episode source)
    {
        var merged = false;

        if (target.Urls.Spotify == null && source.Urls.Spotify != null)
        {
            target.Urls.Spotify = source.Urls.Spotify;
            merged = true;
        }

        if (target.Urls.Apple == null && source.Urls.Apple != null)
        {
            target.Urls.Apple = source.Urls.Apple;
            merged = true;
        }

        if (target.Urls.YouTube == null && source.Urls.YouTube != null)
        {
            target.Urls.YouTube = source.Urls.YouTube;
            merged = true;
        }

        if (target.Urls.BBC == null && source.Urls.BBC != null)
        {
            target.Urls.BBC = source.Urls.BBC;
            merged = true;
        }

        if (target.Urls.InternetArchive == null && source.Urls.InternetArchive != null)
        {
            target.Urls.InternetArchive = source.Urls.InternetArchive;
            merged = true;
        }

        return merged;
    }

    private static bool HasMeaningfulDifferences(
        Dictionary<string, string>? firstDoc,
        Dictionary<string, string>? secondDoc,
        out List<string> differences)
    {
        differences = [];
        if (firstDoc == null || secondDoc == null)
            return false;

        var allKeys = firstDoc.Keys
            .Union(secondDoc.Keys, StringComparer.Ordinal)
            .Where(k => !ExcludedComparisonFields.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal);

        foreach (var key in allKeys)
        {
            var inFirst = firstDoc.TryGetValue(key, out var firstValue);
            var inSecond = secondDoc.TryGetValue(key, out var secondValue);

            if (!inFirst)
                differences.Add($"{key}:<absent>|{secondValue}");
            else if (!inSecond)
                differences.Add($"{key}:{firstValue}|<absent>");
            else if (!string.Equals(firstValue, secondValue, StringComparison.Ordinal))
                differences.Add($"{key}:{firstValue}|{secondValue}");
        }

        return differences.Count > 0;
    }

    // Picks the duration from whichever of the two episodes has the primary-post-service ID present.
    // Falls back to YouTube -> Spotify -> Apple if podcast has no PrimaryPostService set.
    // Returns null when both or neither have the service (no correction possible without an API call).
    private static TimeSpan? GetCorrectDuration(Episode first, Episode second, Podcast? keeperPodcast)
    {
        Service? effectiveService = keeperPodcast?.PrimaryPostService;
        if (!effectiveService.HasValue)
        {
            if (HasYouTubeId(first) || HasYouTubeId(second)) effectiveService = Service.YouTube;
            else if (HasSpotifyId(first) || HasSpotifyId(second)) effectiveService = Service.Spotify;
            else if (HasAppleId(first) || HasAppleId(second)) effectiveService = Service.Apple;
        }

        if (!effectiveService.HasValue)
            return null;

        var firstHas = effectiveService switch
        {
            Service.YouTube => HasYouTubeId(first),
            Service.Spotify => HasSpotifyId(first),
            Service.Apple => HasAppleId(first),
            _ => false
        };

        var secondHas = effectiveService switch
        {
            Service.YouTube => HasYouTubeId(second),
            Service.Spotify => HasSpotifyId(second),
            Service.Apple => HasAppleId(second),
            _ => false
        };

        // Only one episode has this service — trust its duration
        if (firstHas && !secondHas) return first.Length;
        if (secondHas && !firstHas) return second.Length;

        // Both or neither: no service-authoritative correction available
        return null;
    }

    private static bool HasYouTubeId(Episode e) => !string.IsNullOrWhiteSpace(e.YouTubeId);
    private static bool HasSpotifyId(Episode e) => !string.IsNullOrWhiteSpace(e.SpotifyId);
    private static bool HasAppleId(Episode e) => e.AppleId.HasValue;

    private async Task<Dictionary<string, string>?> FetchEpisodeAsDocument(Container container, string id)
    {
        var queryDef = new QueryDefinition("SELECT * FROM episodes e WHERE e.id = @id")
            .WithParameter("@id", id);
        var iter = container.GetItemQueryStreamIterator(queryDef);
        while (iter.HasMoreResults)
        {
            using var response = await iter.ReadNextAsync();
            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = await JsonDocument.ParseAsync(response.Content);
            if (doc.RootElement.TryGetProperty("Documents", out var documents) &&
                documents.ValueKind == JsonValueKind.Array)
            {
                foreach (var document in documents.EnumerateArray())
                {
                    return document.EnumerateObject()
                        .ToDictionary(p => p.Name, p => p.Value.GetRawText(), StringComparer.Ordinal);
                }
            }
        }

        return null;
    }

    private static string? CreateDuplicateFingerprint(EpisodeDuplicateSample item)
    {
        if (!string.IsNullOrWhiteSpace(item.SpotifyId))
            return $"spotify:{item.SpotifyId}";

        if (item.AppleId.HasValue)
            return $"apple:{item.AppleId.Value}";

        if (!string.IsNullOrWhiteSpace(item.YouTubeId))
            return $"youtube:{item.YouTubeId}";

        if (string.IsNullOrWhiteSpace(item.PodcastId) || string.IsNullOrWhiteSpace(item.Title) ||
            !item.Release.HasValue)
            return null;

        return $"fallback:{item.PodcastId}|{item.Title.Trim().ToUpperInvariant()}|{item.Release.Value:O}";
    }

    private CosmosClient CreateCosmosClient()
    {
        var options = new CosmosClientOptions();
        if (_cosmosDbSettings.UseGateway == true)
            options.ConnectionMode = ConnectionMode.Gateway;

        return new CosmosClient(_cosmosDbSettings.Endpoint, _cosmosDbSettings.AuthKeyOrResourceToken, options);
    }

    private sealed record DedupPairReport(
        Guid KeeperId,
        string KeeperTitle,
        string? KeeperPodcastName,
        bool KeeperWasPublished,
        Guid ToDeleteId,
        string ToDeleteTitle,
        IReadOnlyList<string> Differences,
        string Outcome);

    private sealed class CanonicalEpisodeSample
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("podcastName")]
        public string? PodcastName { get; set; }

        [JsonPropertyName("ignored")]
        public bool Ignored { get; set; }

        [JsonPropertyName("removed")]
        public bool Removed { get; set; }
    }

    private sealed class EpisodeDuplicateSample
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("podcastId")]
        public string PodcastId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("release")]
        public DateTime? Release { get; set; }

        [JsonPropertyName("spotifyId")]
        public string? SpotifyId { get; set; }

        [JsonPropertyName("appleId")]
        public long? AppleId { get; set; }

        [JsonPropertyName("youTubeId")]
        public string? YouTubeId { get; set; }

        [JsonPropertyName("podcastName")]
        public string? PodcastName { get; set; }
    }
}
