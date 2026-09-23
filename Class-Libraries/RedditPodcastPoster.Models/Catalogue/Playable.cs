using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Parent playable (Episode / TvShowEpisode / NewsReport): titled production with
/// shared playable and promotion contracts.
/// </summary>
public abstract class Playable : CosmosSelector, IMediaProduction, IPlayable, IPromotable
{
    [JsonPropertyName("title")]
    [JsonPropertyOrder(10)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(20)]
    public string Description { get; set; } = string.Empty;

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

    /// <summary>
    /// Legacy Cosmos flag (<c>bluesky</c>). Populated only by deserialization of pre-migration
    /// documents. Do not set to <c>true</c> in application code — store
    /// <see cref="BlueskyPost"/> after a network post.
    /// </summary>
    [JsonPropertyName("bluesky")]
    [JsonPropertyOrder(42)]
    public bool? OldBlueskyPosted { get; set; }

    /// <summary>
    /// AT URI of the Bluesky post (<c>at://{did}/app.bsky.feed.post/{rkey}</c>).
    /// </summary>
    [JsonPropertyName("blueskyPost")]
    [JsonPropertyOrder(42)]
    public string? BlueskyPost { get; set; }

    [JsonIgnore]
    public bool BlueskyPosted =>
        OldBlueskyPosted == true || !string.IsNullOrWhiteSpace(BlueskyPost);

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(43)]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(44)]
    public bool Removed { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(45)]
    public string? Language { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("removedSubjects")]
    [JsonPropertyOrder(71)]
    public List<string> RemovedSubjects { get; set; } = [];

    [JsonPropertyName("matches")]
    [JsonPropertyOrder(72)]
    public List<PlayableSubjectMatch> Matches { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }

    /// <summary>
    /// Optional playable-level hashtag appended to Tweet/Bluesky posts (e.g. <c>#MyTag</c>).
    /// </summary>
    [JsonPropertyName("hashTag")]
    [JsonPropertyOrder(81)]
    public string? HashTag { get; set; }

    /// <summary>
    /// Denormalised parent publisher search terms. Subclasses set Cosmos JSON names
    /// (<c>podcastSearchTerms</c> / <c>tvShowSearchTerms</c> / <c>newsOrganisationSearchTerms</c>).
    /// Not used on <see cref="Films.Film"/> (Film uses <see cref="Publisher.SearchTerms"/>).
    /// </summary>
    [JsonIgnore]
    public virtual string? PublisherSearchTerms { get; set; }

    /// <summary>
    /// Denormalised parent publisher language. Subclasses set Cosmos JSON names
    /// (<c>podcastLanguage</c> / <c>tvShowLanguage</c> / <c>newsOrganisationLanguage</c>).
    /// Not used on <see cref="Films.Film"/> (Film uses <see cref="Publisher.Language"/>).
    /// </summary>
    [JsonIgnore]
    public virtual string? PublisherLanguage { get; set; }

    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    [JsonPropertyName("guests")]
    [JsonPropertyOrder(160)]
    public string[]? Guests { get; set; }

    public bool IsRemoved() => Removed == true;

    public void ClearBlueskyPostState()
    {
        BlueskyPost = null;
        OldBlueskyPosted = null;
    }

    /// <summary>
    /// Cosmos SQL: playable is Bluesky-posted. Do not use null-only checks — combine
    /// <c>IS_DEFINED</c> with value comparison.
    /// </summary>
    public const string CosmosIsBlueskyPostedSql =
        "((IS_DEFINED(e.bluesky) AND e.bluesky = true) OR (IS_DEFINED(e.blueskyPost) AND NOT IS_NULL(e.blueskyPost)))";

    /// <summary>
    /// Cosmos SQL: playable is not Bluesky-posted.
    /// </summary>
    public const string CosmosIsNotBlueskyPostedSql =
        "((NOT IS_DEFINED(e.bluesky) OR e.bluesky != true) AND (NOT IS_DEFINED(e.blueskyPost) OR IS_NULL(e.blueskyPost)))";
}
