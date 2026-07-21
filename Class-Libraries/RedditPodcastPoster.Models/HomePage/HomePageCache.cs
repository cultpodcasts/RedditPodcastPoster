using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.HomePageCache)]
public sealed class HomePageCache : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("b2e4f6a8-c0d2-4e6f-8a1b-3c5d7e9f0b2d");

    public HomePageCache()
    {
        Id = _Id;
        ModelType = ModelType.HomePageCache;
    }

    [JsonPropertyName("totalDuration")]
    [JsonPropertyOrder(10)]
    public TimeSpan TotalDuration { get; set; }

    [JsonPropertyName("activeEpisodeCount")]
    [JsonPropertyOrder(11)]
    public int? ActiveEpisodeCount { get; set; }

    public override string FileKey => nameof(HomePageCache);
}
