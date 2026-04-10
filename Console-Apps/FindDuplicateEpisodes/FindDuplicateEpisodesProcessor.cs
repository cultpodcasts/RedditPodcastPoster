using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence;

namespace FindDuplicateEpisodes;

public class FindDuplicateEpisodesProcessor(
    IOptions<CosmosDbSettings> cosmosDbSettings,
    ILogger<FindDuplicateEpisodesProcessor> logger)
{
    private const string ActiveEpisodesFilter =
        "((NOT IS_DEFINED(e.podcastRemoved)) OR e.podcastRemoved=false) and ((NOT IS_DEFINED(e.removed)) OR e.removed=false)";

    private static readonly HashSet<string> ExcludedComparisonFields =
        new(StringComparer.Ordinal) { "id", "_rid", "_self", "_etag", "_attachments", "_ts" };

    private readonly CosmosDbSettings _cosmosDbSettings = cosmosDbSettings.Value;

    public async Task Run()
    {
        using var cosmosClient = CreateCosmosClient();
        var container = cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.EpisodesContainer);

        var query = $@"SELECT e.id, e.podcastId, e.title, e.release, e.spotifyId, e.appleId, e.youTubeId, e.podcastName
                       FROM episodes e
                       WHERE {ActiveEpisodesFilter}";
        var iterator = container.GetItemQueryIterator<EpisodeDuplicateSample>(new QueryDefinition(query));
        var seen = new Dictionary<string, EpisodeDuplicateSample>(StringComparer.Ordinal);
        var duplicatePairs = new List<(string FirstId, string SecondId)>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                var fingerprint = CreateDuplicateFingerprint(item);
                if (string.IsNullOrWhiteSpace(fingerprint))
                {
                    continue;
                }

                if (seen.TryGetValue(fingerprint, out var existing) && existing.Id != item.Id)
                {
                    logger.LogWarning(
                        "Potential duplicate episode pair: Fingerprint={Fingerprint}; First={FirstId}/{FirstPodcastId}/{FirstPodcastName}/{FirstTitle}/{FirstRelease}; Second={SecondId}/{SecondPodcastId}/{SecondPodcastName}/{SecondTitle}/{SecondRelease}",
                        fingerprint,
                        existing.Id,
                        existing.PodcastId,
                        existing.PodcastName ?? string.Empty,
                        existing.Title,
                        existing.Release,
                        item.Id,
                        item.PodcastId,
                        item.PodcastName ?? string.Empty,
                        item.Title,
                        item.Release);
                    duplicatePairs.Add((existing.Id, item.Id));
                }

                seen[fingerprint] = item;
            }
        }

        foreach (var (firstId, secondId) in duplicatePairs)
        {
            await LogDuplicateFieldDifferences(container, firstId, secondId);
        }

        if (duplicatePairs.Count == 0)
        {
            logger.LogInformation("No potential duplicate episode pairs found.");
        }
        else
        {
            logger.LogWarning("Found {Count} potential duplicate pair(s). Run FindDuplicateEpisodes for detailed field comparison.", duplicatePairs.Count);
        }
    }

    private async Task LogDuplicateFieldDifferences(Container container, string firstId, string secondId)
    {
        var firstDoc = await FetchEpisodeAsDocument(container, firstId);
        var secondDoc = await FetchEpisodeAsDocument(container, secondId);

        if (firstDoc == null || secondDoc == null)
        {
            logger.LogWarning(
                "Duplicate pair {FirstId}/{SecondId}: unable to fetch full documents for field comparison.",
                firstId,
                secondId);
            return;
        }

        var allKeys = firstDoc.Keys
            .Union(secondDoc.Keys, StringComparer.Ordinal)
            .Where(k => !ExcludedComparisonFields.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal);

        var differences = new List<string>();
        foreach (var key in allKeys)
        {
            var inFirst = firstDoc.TryGetValue(key, out var firstValue);
            var inSecond = secondDoc.TryGetValue(key, out var secondValue);

            if (!inFirst)
            {
                differences.Add($"{key}:<absent>|{secondValue}");
            }
            else if (!inSecond)
            {
                differences.Add($"{key}:{firstValue}|<absent>");
            }
            else if (!string.Equals(firstValue, secondValue, StringComparison.Ordinal))
            {
                differences.Add($"{key}:{firstValue}|{secondValue}");
            }
        }

        if (differences.Count > 0)
        {
            logger.LogWarning(
                "Duplicate pair {FirstId}/{SecondId} has {DifferenceCount} field difference(s): {Differences}",
                firstId,
                secondId,
                differences.Count,
                string.Join("; ", differences));
        }
        else
        {
            logger.LogWarning(
                "Duplicate pair {FirstId}/{SecondId}: all comparable fields are identical.",
                firstId,
                secondId);
        }
    }

    private async Task<Dictionary<string, string>?> FetchEpisodeAsDocument(Container container, string id)
    {
        var queryDef = new QueryDefinition("SELECT * FROM episodes e WHERE e.id = @id")
            .WithParameter("@id", id);
        var iter = container.GetItemQueryStreamIterator(queryDef);
        while (iter.HasMoreResults)
        {
            using var response = await iter.ReadNextAsync();
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

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
        {
            return $"spotify:{item.SpotifyId}";
        }

        if (item.AppleId.HasValue)
        {
            return $"apple:{item.AppleId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(item.YouTubeId))
        {
            return $"youtube:{item.YouTubeId}";
        }

        if (string.IsNullOrWhiteSpace(item.PodcastId) || string.IsNullOrWhiteSpace(item.Title) ||
            !item.Release.HasValue)
        {
            return null;
        }

        return $"fallback:{item.PodcastId}|{item.Title.Trim().ToUpperInvariant()}|{item.Release.Value:O}";
    }

    private CosmosClient CreateCosmosClient()
    {
        var options = new CosmosClientOptions();
        if (_cosmosDbSettings.UseGateway == true)
        {
            options.ConnectionMode = ConnectionMode.Gateway;
        }

        return new CosmosClient(_cosmosDbSettings.Endpoint, _cosmosDbSettings.AuthKeyOrResourceToken, options);
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
