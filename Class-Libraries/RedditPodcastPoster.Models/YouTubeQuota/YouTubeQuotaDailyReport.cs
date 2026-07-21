using System.Text.Json.Serialization;

using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Models.YouTubeQuota;

/// <summary>
///     A single day's quota snapshot, embedded in the rolling <see cref="YouTubeQuotaReport" /> document.
/// </summary>
public sealed class YouTubeQuotaDailyReport
{
    [JsonPropertyName("reportDate")]
    [JsonPropertyOrder(10)]
    public DateOnly ReportDate { get; set; }

    [JsonPropertyName("sourceApplication")]
    [JsonPropertyOrder(11)]
    public required string SourceApplication { get; set; }

    [JsonPropertyName("keys")]
    [JsonPropertyOrder(12)]
    public List<YouTubeQuotaKeyStats> Keys { get; set; } = [];

    [JsonPropertyName("usedIndexerKeys")]
    [JsonPropertyOrder(13)]
    public List<YouTubeIndexerKeySummary> UsedIndexerKeys { get; set; } = [];

    [JsonPropertyName("unusedIndexerKeys")]
    [JsonPropertyOrder(14)]
    public List<YouTubeIndexerKeySummary> UnusedIndexerKeys { get; set; } = [];

    [JsonPropertyName("podcastsNotIndexedDueToQuota")]
    [JsonPropertyOrder(15)]
    public int PodcastsNotIndexedDueToQuota { get; set; }

    [JsonPropertyName("podcastsNotEnrichedDueToQuota")]
    [JsonPropertyOrder(16)]
    public int PodcastsNotEnrichedDueToQuota { get; set; }

    [JsonPropertyName("ringExhaustionCount")]
    [JsonPropertyOrder(17)]
    public int RingExhaustionCount { get; set; }

    [JsonPropertyName("nonQuotaErrorCount")]
    [JsonPropertyOrder(18)]
    public int NonQuotaErrorCount { get; set; }
}

public sealed class YouTubeQuotaKeyStats
{
    [JsonPropertyName("displayName")]
    [JsonPropertyOrder(10)]
    public required string DisplayName { get; set; }

    [JsonPropertyName("project")]
    [JsonPropertyOrder(11)]
    public required string Project { get; set; }

    [JsonPropertyName("usage")]
    [JsonPropertyOrder(12)]
    public required string Usage { get; set; }

    [JsonPropertyName("callsAttempted")]
    [JsonPropertyOrder(13)]
    public int CallsAttempted { get; set; }

    [JsonPropertyName("quotaHits")]
    [JsonPropertyOrder(14)]
    public int QuotaHits { get; set; }

    [JsonPropertyName("quotaUsed")]
    [JsonPropertyOrder(15)]
    public int QuotaUsed { get; set; }

    [JsonPropertyName("estimatedQuotaUsed")]
    [JsonPropertyOrder(16)]
    public int EstimatedQuotaUsed { get; set; }

    [JsonPropertyName("dailyLimit")]
    [JsonPropertyOrder(17)]
    public int DailyLimit { get; set; }

    [JsonPropertyName("quotaConsumedByOperation")]
    [JsonPropertyOrder(18)]
    public YouTubeQuotaConsumedByOperation QuotaConsumedByOperation { get; set; } = new();

    [JsonPropertyName("capacityHint")]
    [JsonPropertyOrder(19)]
    public string? CapacityHint { get; set; }
}

public sealed class YouTubeIndexerKeySummary
{
    [JsonPropertyName("displayName")]
    [JsonPropertyOrder(10)]
    public required string DisplayName { get; set; }

    [JsonPropertyName("project")]
    [JsonPropertyOrder(11)]
    public required string Project { get; set; }

    [JsonPropertyName("hourPrimary")]
    [JsonPropertyOrder(12)]
    public int HourPrimary { get; set; }

    [JsonPropertyName("reattempt")]
    [JsonPropertyOrder(13)]
    public int? Reattempt { get; set; }

    [JsonPropertyName("apiKeySuffix")]
    [JsonPropertyOrder(14)]
    public required string ApiKeySuffix { get; set; }

    [JsonPropertyName("callsAttempted")]
    [JsonPropertyOrder(15)]
    public int CallsAttempted { get; set; }

    [JsonPropertyName("quotaHits")]
    [JsonPropertyOrder(16)]
    public int QuotaHits { get; set; }

    [JsonPropertyName("quotaUsed")]
    [JsonPropertyOrder(17)]
    public int QuotaUsed { get; set; }

    [JsonPropertyName("estimatedQuotaUsed")]
    [JsonPropertyOrder(18)]
    public int EstimatedQuotaUsed { get; set; }

    [JsonPropertyName("dailyLimit")]
    [JsonPropertyOrder(19)]
    public int DailyLimit { get; set; }

    [JsonPropertyName("quotaConsumedByOperation")]
    [JsonPropertyOrder(20)]
    public YouTubeQuotaConsumedByOperation QuotaConsumedByOperation { get; set; } = new();
}

public sealed class YouTubeQuotaConsumedByOperation
{
    [JsonPropertyName("searchList")]
    [JsonPropertyOrder(10)]
    public int SearchList { get; set; }

    [JsonPropertyName("channelsList")]
    [JsonPropertyOrder(11)]
    public int ChannelsList { get; set; }

    [JsonPropertyName("playlistItemsList")]
    [JsonPropertyOrder(12)]
    public int PlaylistItemsList { get; set; }

    [JsonPropertyName("playlistsList")]
    [JsonPropertyOrder(13)]
    public int PlaylistsList { get; set; }

    [JsonPropertyName("videosList")]
    [JsonPropertyOrder(14)]
    public int VideosList { get; set; }
}
