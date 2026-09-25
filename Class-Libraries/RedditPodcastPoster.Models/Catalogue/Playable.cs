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
    /// <see cref="ReleaseSort"/> and <see cref="ReleaseCosmosFallback"/> to the same UTC instant.
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
            var sortUtc = value?.ToSortUtc() ?? default;
            ReleaseSort = sortUtc;
            ReleaseCosmosFallback = sortUtc;
        }
    }

    /// <summary>
    /// UTC instant for Cosmos range filters and ordering. Synced from <see cref="Release"/>
    /// (and from STJ populate of <c>releaseSort</c>). Private set so callers cannot desync it
    /// from <see cref="Release"/> — use <see cref="SetRelease"/>.
    /// Until the corpus is backfilled, server-side range filters must dual-key with
    /// <see cref="ReleaseCosmosFallback"/> (legacy docs may only have <c>release</c>).
    /// </summary>
    [JsonPropertyName("releaseSort")]
    [JsonPropertyOrder(31)]
    public DateTime ReleaseSort { get; private set; }

    /// <summary>
    /// Cosmos LINQ-only dual-key companion mapping to JSON <c>release</c> as <see cref="DateTime"/>.
    /// STJ ignores this member so it does not conflict with <see cref="Release"/>;
    /// <c>CosmosLinqSerializer.SerializeMemberName</c> still emits <c>release</c> from
    /// <see cref="JsonPropertyNameAttribute"/>. Use in server-side predicates:
    /// <c>(ReleaseSort.IsDefined() ? ReleaseSort : ReleaseCosmosFallback) &gt;= since</c>
    /// until every matching document has <c>releaseSort</c>. Not for application reads.
    /// </summary>
    [JsonPropertyName("release")]
    [JsonIgnore]
    public DateTime ReleaseCosmosFallback { get; private set; }

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
    /// Denormalised parent publisher description (JSON <c>publisherDescription</c>).
    /// Search <c>seriesDescription</c> is this text after the shared search truncation.
    /// Not used on <see cref="Films.Film"/>.
    /// </summary>
    [JsonPropertyName("publisherDescription")]
    [JsonPropertyOrder(95)]
    public string? PublisherDescription { get; set; }

    /// <summary>
    /// Denormalised parent publisher Cosmos <c>_ts</c> (JSON <c>parentMetadataVersion</c>).
    /// Used to detect stale parent projection. Not used on <see cref="Films.Film"/>.
    /// </summary>
    [JsonPropertyName("parentMetadataVersion")]
    [JsonPropertyOrder(93)]
    public long? ParentMetadataVersion { get; set; }

    /// <summary>
    /// Denormalised parent publisher <see cref="Publisher.Removed"/> (JSON <c>parentRemoved</c>).
    /// Not used on <see cref="Films.Film"/>.
    /// Until the Episode corpus is rewritten, Cosmos SQL/LINQ must also treat legacy
    /// <c>podcastRemoved</c> (see <see cref="CosmosParentNotRemovedSql"/>) — deserialize
    /// bridges alone are not query-safe.
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

    /// <summary>
    /// Copy a parent publisher description onto <see cref="PublisherDescription"/>.
    /// Blank parent text clears the denormalised blurb.
    /// </summary>
    protected bool AssignPublisherDescription(string? parentDescription)
    {
        var next = string.IsNullOrWhiteSpace(parentDescription) ? null : parentDescription.Trim();
        if (PublisherDescription == next)
        {
            return false;
        }

        PublisherDescription = next;
        return true;
    }

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
    /// Cosmos SQL expression: effective release instant — <c>releaseSort</c> when present,
    /// else legacy <c>release</c>. Alias <c>e</c>. Use for range filters until backfill.
    /// </summary>
    public const string CosmosReleaseSortOrReleaseSql =
        "(IS_DEFINED(e.releaseSort) ? e.releaseSort : e.release)";

    /// <summary>
    /// Cosmos SQL: parent publisher is not removed — dual-key <c>parentRemoved</c> and legacy
    /// <c>podcastRemoved</c>. Alias <c>e</c>. Deserialize bridges do not satisfy this predicate.
    /// </summary>
    public const string CosmosParentNotRemovedSql =
        "((NOT IS_DEFINED(e.parentRemoved) OR e.parentRemoved=false) AND (NOT IS_DEFINED(e.podcastRemoved) OR e.podcastRemoved=false))";
}
