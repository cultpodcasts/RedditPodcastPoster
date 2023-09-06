using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.EliminationTerms;

[CosmosSelector(ModelType.EliminationTerms)]
public class EliminationTerms : CosmosSelector
{
    public static Guid _Id = Guid.Parse("D7B683EC-4948-44C4-B7BD-FB382CD3B1B6");

    public EliminationTerms() : base(ModelType.EliminationTerms)
    {
    }

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; private set; } = _Id;

    [JsonPropertyName("terms")]
    [JsonPropertyOrder(10)]
    public List<string> Terms { get; set; } = new();
}