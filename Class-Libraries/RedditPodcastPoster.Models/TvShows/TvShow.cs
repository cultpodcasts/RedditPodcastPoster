using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.TvShows;

[CosmosSelector(ModelType.TvShow)]
public sealed class TvShow : CosmosSelector
{
    public TvShow()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.TvShow;
    }

    public TvShow(string name) : this()
    {
        Name = name;
        FileKey = FileKeyFactory.GetFileKey(name);
    }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(20)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(21)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(22)]
    public string? Language { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(25)]
    public bool? Removed { get; set; }

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }
}
