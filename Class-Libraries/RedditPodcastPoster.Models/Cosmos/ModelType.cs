using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Cosmos;

public enum ModelType
{
    [JsonPropertyName("podcast")]
    Podcast = 1,

    [JsonPropertyName("episode")]
    Episode = 2,

    [JsonPropertyName(nameof(EliminationTerms))]
    EliminationTerms = 3,

    [JsonPropertyName(nameof(RedditPost))]
    RedditPost = 4,

    [JsonPropertyName(nameof(KnownTerms))]
    KnownTerms = 5,

    [JsonPropertyName(nameof(TrainingData))]
    TrainingData = 6,

    [JsonPropertyName(nameof(Subject))]
    Subject = 7,

    [JsonPropertyName(nameof(Activity))]
    Activity = 8,

    [JsonPropertyName(nameof(Discovery))]
    Discovery = 9,

    [JsonPropertyName(nameof(PushSubscription))]
    PushSubscription = 10,

    [JsonPropertyName(nameof(HomePageCache))]
    HomePageCache = 11,

    [JsonPropertyName(nameof(YouTubeQuotaReport))]
    YouTubeQuotaReport = 12,

    [JsonPropertyName(nameof(YouTubeIndexerKeyState))]
    YouTubeIndexerKeyState = 13,

    [JsonPropertyName(nameof(YouTubeQuotaUsageState))]
    YouTubeQuotaUsageState = 14,

    [JsonPropertyName(nameof(Person))]
    Person = 15,

    [JsonPropertyName(nameof(DiscoveryScheduleConfig))]
    DiscoveryScheduleConfig = 16
}
