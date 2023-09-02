using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class CosmosSelector
{
    public CosmosSelector(ModelType modelType)
    {
        ModelType = modelType;
    }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModelType ModelType { get; set; }
}