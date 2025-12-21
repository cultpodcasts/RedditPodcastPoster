using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Text.KnownTerms;

[CosmosSelector(ModelType.KnownTerms)]
public sealed class KnownTerms : CosmosSelector
{
    public static Guid _Id = Guid.Parse("4B550528-6848-4877-A768-0DA301754C7B");

    public KnownTerms()
    {
        Id = _Id;
        ModelType = ModelType.KnownTerms;
    }

    [JsonPropertyName("terms")]
    [JsonPropertyOrder(10)]
    public Dictionary<string, Regex> Terms { get; set; } = new();

    public override string FileKey => nameof(KnownTerms);
}