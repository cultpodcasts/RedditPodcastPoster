using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.Films;

/// <summary>
/// Standalone made-as-film playable (no parent). ADR-0002 / epic S-001…S-002.
/// Platform presence is <see cref="Services"/> (URLs/images). Compact <see cref="YouTubeId"/>
/// is synced from <c>services.youtube.url</c> for search; no Spotify/Apple id members.
/// Canonical identity is <see cref="IFilmCanonical.ImdbId"/> (not a streaming service).
/// Release is year or calendar date — not a podcast-episode datetime.
/// Display name is <see cref="Publisher.Name"/> (JSON <c>name</c>) — no <see cref="IMediaProduction.Title"/>.
/// Description / Language / SearchTerms / HashTag come from <see cref="Publisher"/>
/// (satisfy <see cref="IPlayable"/> / <see cref="IPromotable"/>).
/// </summary>
[CosmosSelector(ModelType.Film)]
public sealed class Film : Publisher, IPlayable, ICatalogueYouTubeItem, IPromotable, IFilmCanonical
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

    private CatalogueRelease? _release;

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
    /// UTC sort key; synced from <see cref="Release"/>. Private set — use <see cref="SetRelease"/>.
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

    [JsonPropertyName("bluesky")]
    [JsonPropertyOrder(42)]
    public bool? OldBlueskyPosted { get; set; }

    [JsonPropertyName("blueskyPost")]
    [JsonPropertyOrder(43)]
    public string? BlueskyPost { get; set; }

    [JsonIgnore]
    public bool BlueskyPosted =>
        OldBlueskyPosted == true || !string.IsNullOrWhiteSpace(BlueskyPost);

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(44)]
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

    /// <summary>IMDb title id (<c>tt…</c>) for this film. Omitted when unknown.</summary>
    [JsonPropertyName("imdbId")]
    [JsonPropertyOrder(143)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImdbId { get; set; }

    /// <summary>TMDB id for this film. Omitted when unknown. Films have no TheTVDB id.</summary>
    [JsonPropertyName("tmdbId")]
    [JsonPropertyOrder(142)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TmdbId { get; set; }

    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    /// <summary>
    /// Compact YouTube video id. Synced from <c>services.youtube.url</c> on catalogue edits
    /// so Azure Search can project <c>youtubeId</c>. No Spotify/Apple id members.
    /// </summary>
    private string? _youTubeId;

    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(152)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? YouTubeId
    {
        get => _youTubeId;
        set => _youTubeId = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Trailers, teasers and clips of this film, in display order. Omitted when there are none.
    /// Each one is a whole video of its own, unlike <see cref="Services"/>, which holds one link per platform.
    /// </summary>
    [JsonPropertyName("trailers")]
    [JsonPropertyOrder(153)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FilmTrailer>? Trailers { get; set; }

    [JsonPropertyName("guests")]
    [JsonPropertyOrder(160)]
    public string[]? Guests { get; set; }

    public void SetRelease(CatalogueRelease? release) => Release = release;

    /// <summary>True when a trailer came from the episode with this id.</summary>
    public bool HasTrailer(Guid episodeId) => Trailers?.Any(trailer => trailer.Id == episodeId) == true;

    /// <summary>Same curator subject rule as <see cref="Playable.ApplyUserSubjects"/>.</summary>
    public bool ApplyUserSubjects(IEnumerable<string> newSubjects)
    {
        var (changed, subjects) = CuratorSubjects.Apply(Subjects, RemovedSubjects, Matches, newSubjects);
        Subjects = subjects;
        return changed;
    }

    public void ClearBlueskyPostState()
    {
        BlueskyPost = null;
        OldBlueskyPosted = null;
    }
}
