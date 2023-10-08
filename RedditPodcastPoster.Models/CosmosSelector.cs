using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class CosmosSelector
{
    protected CosmosSelector(ModelType modelType)
    {
        ModelType = modelType;
    }

    public string GetPartitionKey()
    {
        return this.ModelType.ToString();
    }

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModelType ModelType { get; set; }
}