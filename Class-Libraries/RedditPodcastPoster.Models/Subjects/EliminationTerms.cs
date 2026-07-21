using System.Text.Json.Serialization;

using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Models.Subjects;

[CosmosSelector(ModelType.EliminationTerms)]
public sealed class EliminationTerms : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("D7B683EC-4948-44C4-B7BD-FB382CD3B1B6");

    public EliminationTerms()
    {
        Id = _Id;
        ModelType = ModelType.EliminationTerms;
    }

    [JsonPropertyName("terms")]
    [JsonPropertyOrder(10)]
    public List<string> Terms { get; set; } = new();

    public override string FileKey => nameof(EliminationTerms);
}