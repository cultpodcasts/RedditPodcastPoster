using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.EliminationTerms)]
public class EliminationTerms : CosmosSelector
{
    public static readonly string PartitionKey = ModelType.EliminationTerms.ToString();

    public static Guid _Id = Guid.Parse("D7B683EC-4948-44C4-B7BD-FB382CD3B1B6");

    public EliminationTerms() : base(_Id, ModelType.EliminationTerms)
    {
    }

    [JsonPropertyName("terms")]
    [JsonPropertyOrder(10)]
    public List<string> Terms { get; set; } = new();

    public override string FileKey => nameof(EliminationTerms);
}