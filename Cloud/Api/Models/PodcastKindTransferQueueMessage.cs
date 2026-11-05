using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Models;

public sealed class PodcastKindTransferQueueMessage
{
    public const string QueueName = "podcast-kind-transfer";

    [JsonPropertyName("podcastId")]
    public Guid PodcastId { get; init; }

    [JsonPropertyName("targetKind")]
    public CatalogueParentKind TargetKind { get; init; }

    [JsonPropertyName("enqueuedUtc")]
    public DateTime EnqueuedUtc { get; init; }
}
