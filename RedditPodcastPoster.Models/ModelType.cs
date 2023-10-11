using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

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
    TrainingData = 6

}