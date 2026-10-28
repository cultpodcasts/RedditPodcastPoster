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

    private CatalogueRelease? _release;

    /// <summary>
    /// Semantic release (year / date / UTC datetime). Setting this updates
    /// <see cref="ReleaseSort"/> to the same UTC instant.
    /// Prefer <see cref="SetRelease"/> at call sites.
    /// </summary>
    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public CatalogueRelease? Release
    {
        get => _release;
        set
        {
            _release = value;
            ReleaseSort = value?.ToSortUtc() ?? default;
        }
    }

    /// <summary>
    /// UTC instant for Cosmos range filters and ordering. Synced from <see cref="Release"/>
    /// (and from STJ populate of <c>releaseSort</c>). Private set so callers cannot desync it
    /// from <see cref="Release"/> — use <see cref="SetRelease"/>.
    /// Every stored playable carries <c>releaseSort</c> (GATE 1 wire rewrite), so server-side
    /// range filters use it alone.
    /// </summary>
    [JsonPropertyName("releaseSort")]
    [JsonPropertyOrder(31)]
    public DateTime ReleaseSort { get; private set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(32)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(33)]
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
    [JsonPropertyOrder(43)]
    public string? BlueskyPost { get; set; }

    [JsonIgnore]
    public bool BlueskyPosted =>
        OldBlueskyPosted == true || !string.IsNullOrWhiteSpace(BlueskyPost);

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(44)]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(45)]
    public bool Removed { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(46)]
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
    /// Denormalised parent publisher search terms (JSON <c>publisherSearchTerms</c>).
    /// Not used on <see cref="Films.Film"/> (Film uses <see cref="Publisher.SearchTerms"/>).
    /// </summary>
    [JsonPropertyName("publisherSearchTerms")]
    [JsonPropertyOrder(91)]
    public string? PublisherSearchTerms { get; set; }

    /// <summary>
    /// Denormalised parent publisher language (JSON <c>publisherLanguage</c>).
    /// Not used on <see cref="Films.Film"/> (Film uses <see cref="Publisher.Language"/>).
    /// </summary>
    [JsonPropertyName("publisherLanguage")]
    [JsonPropertyOrder(92)]
    public string? PublisherLanguage { get; set; }

    /// <summary>
    /// Denormalised parent publisher Cosmos <c>_ts</c> (JSON <c>parentMetadataVersion</c>).
    /// Used to detect stale parent projection. Not used on <see cref="Films.Film"/>.
    /// </summary>
    [JsonPropertyName("parentMetadataVersion")]
    [JsonPropertyOrder(93)]
    public long? ParentMetadataVersion { get; set; }

    /// <summary>
    /// Denormalised parent publisher <see cref="Publisher.Removed"/> (JSON <c>parentRemoved</c>).
    /// Not used on <see cref="Films.Film"/>. Server-side filters use
    /// <see cref="CosmosParentNotRemovedSql"/>.
    /// </summary>
    [JsonPropertyName("parentRemoved")]
    [JsonPropertyOrder(94)]
    public bool? ParentRemoved { get; set; }

    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    [JsonPropertyName("guests")]
    [JsonPropertyOrder(160)]
    public string[]? Guests { get; set; }

    public bool IsRemoved() => Removed == true;

    public void SetRelease(CatalogueRelease? release) => Release = release;

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

    /// <summary>
    /// Cosmos SQL: parent publisher is not removed (<c>parentRemoved</c> absent, null or false). Alias <c>e</c>.
    /// Explicit null counts as not removed, the same as the LINQ <c>EpisodeCosmosFilters.ParentNotRemoved</c>
    /// (in Cosmos SQL <c>null = false</c> is not true, so null needs its own arm).
    /// </summary>
    public const string CosmosParentNotRemovedSql =
        "(NOT IS_DEFINED(e.parentRemoved) OR IS_NULL(e.parentRemoved) OR e.parentRemoved=false)";

    /// <summary>
    /// Applies a curator subject update (episodes, TV episodes, news reports) and maintains <see cref="Playable.RemovedSubjects"/>.
    /// </summary>
    public bool ApplyUserSubjects(IEnumerable<string> newSubjects)
    {
        var (changed, subjects) = CuratorSubjects.Apply(Subjects, RemovedSubjects, Matches, newSubjects);
        Subjects = subjects;
        return changed;
    }
}
