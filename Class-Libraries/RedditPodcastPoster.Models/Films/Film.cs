using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.Films;

/// <summary>
/// Standalone made-as-film playable (no parent). ADR-0002 / epic S-001…S-002.
/// Platform presence is <see cref="Services"/> only — no provider-id fields.
/// Release is year or calendar date — not a podcast-episode datetime.
/// Display name is <see cref="Publisher.Name"/> (JSON <c>name</c>) — no <see cref="IMediaProduction.Title"/>.
/// Description / Language / SearchTerms / HashTag come from <see cref="Publisher"/>
/// (satisfy <see cref="IPlayable"/> / <see cref="IPromotable"/>).
/// </summary>
[CosmosSelector(ModelType.Film)]
public sealed class Film : Publisher, IPlayable, IPromotable
{
    public Film()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.Film;
    }

    public Film(string name) : this()
    {
        Name = name;
        FileKey = FileKeyFactory.GetFilmFileKey(name);
    }

    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public CatalogueRelease? Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(31)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(32)]
    public bool Explicit { get; set; }

    [JsonPropertyName("posted")]
    [JsonPropertyOrder(40)]
    public bool Posted { get; set; }

    [JsonPropertyName("tweeted")]
    [JsonPropertyOrder(41)]
    public bool Tweeted { get; set; }

    [JsonPropertyName("bluesky")]
    [JsonPropertyOrder(42)]
    public bool? OldBlueskyPosted { get; set; }

    [JsonPropertyName("blueskyPost")]
    [JsonPropertyOrder(42)]
    public string? BlueskyPost { get; set; }

    [JsonIgnore]
    public bool BlueskyPosted =>
        OldBlueskyPosted == true || !string.IsNullOrWhiteSpace(BlueskyPost);

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(43)]
    public bool Ignored { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("removedSubjects")]
    [JsonPropertyOrder(71)]
    public List<string> RemovedSubjects { get; set; } = [];

    [JsonPropertyName("matches")]
    [JsonPropertyOrder(72)]
    public List<PlayableSubjectMatch> Matches { get; set; } = [];

    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    [JsonPropertyName("guests")]
    [JsonPropertyOrder(160)]
    public string[]? Guests { get; set; }

    public void ClearBlueskyPostState()
    {
        BlueskyPost = null;
        OldBlueskyPosted = null;
    }
}
