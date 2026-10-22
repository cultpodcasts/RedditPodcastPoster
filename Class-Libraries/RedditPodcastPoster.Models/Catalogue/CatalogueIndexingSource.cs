using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Episode-discovery settings a <see cref="TvShows.TvShow"/> or <see cref="News.NewsOrganisation"/>
/// keeps from the podcast it was migrated from (JSON <c>indexing</c>). Wire names match
/// <see cref="Podcast"/>, so the hourly indexer can run the podcast discovery pipeline on a
/// transient podcast built from the parent. Film has no parent and is never indexed.
/// <para>
/// The string ids and regexes are null when unset: a blank or whitespace value (from a curator clear, a podcast
/// default of <c>""</c>, or a document saved before this rule) is read and set as null and never written, so the
/// stored <c>indexing</c> block only holds the values that are actually set (the Cosmos serializer omits nulls).
/// </para>
/// </summary>
public sealed class CatalogueIndexingSource
{
    private string? _spotifyId;
    private string? _youTubeChannelId;
    private string? _youTubePlaylistId;
    private string? _titleRegex;
    private string? _descriptionRegex;
    private string? _episodeMatchRegex;
    private string? _episodeIncludeTitleRegex;

    [JsonPropertyName("hasBundledEpisodes")]
    public bool Bundles { get; set; }

    [JsonPropertyName("indexAllEpisodes")]
    public bool IndexAllEpisodes { get; set; }

    [JsonPropertyName("ignoreAllEpisodes")]
    public bool? IgnoreAllEpisodes { get; set; }

    [JsonPropertyName("bypassShortEpisodeChecking")]
    public bool? BypassShortEpisodeChecking { get; set; }

    [JsonPropertyName("minimumDuration")]
    public TimeSpan? MinimumDuration { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("spotifyId")]
    public string? SpotifyId
    {
        get => _spotifyId;
        set => _spotifyId = NullIfBlank(value);
    }

    [JsonPropertyName("spotifyMarket")]
    public string? SpotifyMarket { get; set; }

    [JsonPropertyName("spotifyEpisodesQueryIsExpensive")]
    public bool? SpotifyEpisodesQueryIsExpensive { get; set; }

    [JsonPropertyName("spotifyEpisodeCount")]
    public int? SpotifyEpisodeCount { get; set; }

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; }

    [JsonPropertyName("appleEpisodeCount")]
    public int? AppleEpisodeCount { get; set; }

    [JsonPropertyName("youTubeChannelId")]
    public string? YouTubeChannelId
    {
        get => _youTubeChannelId;
        set => _youTubeChannelId = NullIfBlank(value);
    }

    [JsonPropertyName("youTubePlaylistId")]
    public string? YouTubePlaylistId
    {
        get => _youTubePlaylistId;
        set => _youTubePlaylistId = NullIfBlank(value);
    }

    [JsonPropertyName("youTubePlaylistIdHistory")]
    public List<YouTubePlaylistIdHistoryEntry>? YouTubePlaylistIdHistory { get; set; }

    [JsonPropertyName("youTubePlaylistOrder")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlaylistOrder? YouTubePlaylistOrder { get; set; }

    [JsonPropertyName("youTubePublicationOffset")]
    public long? YouTubePublicationOffset { get; set; }

    [JsonPropertyName("youTubePlaylistQueryIsExpensive")]
    public bool? YouTubePlaylistQueryIsExpensive { get; set; }

    [JsonPropertyName("youTubeChannelSearchForbidden")]
    public bool? YouTubeChannelSearchForbidden { get; set; }

    [JsonPropertyName("skipEnrichingFromYouTube")]
    public bool? SkipEnrichingFromYouTube { get; set; }

    [JsonPropertyName("titleRegex")]
    public string? TitleRegex
    {
        get => _titleRegex;
        set => _titleRegex = NullIfBlank(value);
    }

    [JsonPropertyName("descriptionRegex")]
    public string? DescriptionRegex
    {
        get => _descriptionRegex;
        set => _descriptionRegex = NullIfBlank(value);
    }

    [JsonPropertyName("episodeMatchRegex")]
    public string? EpisodeMatchRegex
    {
        get => _episodeMatchRegex;
        set => _episodeMatchRegex = NullIfBlank(value);
    }

    [JsonPropertyName("episodeIncludeTitleRegex")]
    public string? EpisodeIncludeTitleRegex
    {
        get => _episodeIncludeTitleRegex;
        set => _episodeIncludeTitleRegex = NullIfBlank(value);
    }

    /// <summary>Same rule as the podcast indexer: index all episodes, or only titles matching the include regex.</summary>
    [JsonIgnore]
    public bool IsAutoIndexed =>
        IndexAllEpisodes || !string.IsNullOrWhiteSpace(EpisodeIncludeTitleRegex);

    public static CatalogueIndexingSource FromPodcast(Podcast podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        var source = new CatalogueIndexingSource();
        source.CaptureFrom(podcast);
        return source;
    }

    /// <summary>
    /// Copy discovery settings (and the bookmarks the updater mutates, e.g. expensive-query flags
    /// and show episode counts) back from a podcast after an index pass.
    /// </summary>
    public void CaptureFrom(Podcast podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        Bundles = podcast.Bundles;
        IndexAllEpisodes = podcast.IndexAllEpisodes;
        IgnoreAllEpisodes = podcast.IgnoreAllEpisodes;
        BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking;
        MinimumDuration = podcast.MinimumDuration;
        ReleaseAuthority = podcast.ReleaseAuthority;
        PrimaryPostService = podcast.PrimaryPostService;
        SpotifyId = podcast.SpotifyId;
        SpotifyMarket = podcast.SpotifyMarket;
        SpotifyEpisodesQueryIsExpensive = podcast.SpotifyEpisodesQueryIsExpensive;
        SpotifyEpisodeCount = podcast.SpotifyEpisodeCount;
        AppleId = podcast.AppleId;
        AppleEpisodeCount = podcast.AppleEpisodeCount;
        YouTubeChannelId = podcast.YouTubeChannelId;
        YouTubePlaylistId = podcast.YouTubePlaylistId;
        YouTubePlaylistIdHistory = CloneHistory(podcast.YouTubePlaylistIdHistory);
        YouTubePlaylistOrder = podcast.YouTubePlaylistOrder;
        YouTubePublicationOffset = podcast.YouTubePublicationOffset;
        YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive;
        YouTubeChannelSearchForbidden = podcast.YouTubeChannelSearchForbidden;
        SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube;
        TitleRegex = podcast.TitleRegex;
        DescriptionRegex = podcast.DescriptionRegex;
        EpisodeMatchRegex = podcast.EpisodeMatchRegex;
        EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex;
    }

    /// <summary>
    /// A podcast that is never saved: same id as the parent, the parent's publisher fields, and these
    /// discovery settings. Episode providers, mergers and filters take a <see cref="Podcast"/>.
    /// </summary>
    public Podcast ToTransientPodcast(Publisher parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return new Podcast
        {
            Id = parent.Id,
            Name = parent.Name,
            Description = parent.Description,
            LatestReleased = parent.LatestReleased,
            Language = parent.Language,
            LastIndexed = parent.LastIndexed,
            Removed = parent.Removed,
            PublisherName = parent.PublisherName,
            TwitterHandle = parent.TwitterHandle,
            BlueskyHandle = parent.BlueskyHandle,
            HashTag = parent.HashTag,
            EnrichmentHashTags = parent.EnrichmentHashTags is null ? null : [.. parent.EnrichmentHashTags],
            IgnoredAssociatedSubjects = parent.IgnoredAssociatedSubjects is null ? null : [.. parent.IgnoredAssociatedSubjects],
            IgnoredSubjects = parent.IgnoredSubjects is null ? null : [.. parent.IgnoredSubjects],
            DefaultSubject = parent.DefaultSubject,
            SearchTerms = parent.SearchTerms,
            KnownTerms = parent.KnownTerms is null ? null : [.. parent.KnownTerms],
            Bundles = Bundles,
            IndexAllEpisodes = IndexAllEpisodes,
            IgnoreAllEpisodes = IgnoreAllEpisodes,
            BypassShortEpisodeChecking = BypassShortEpisodeChecking,
            MinimumDuration = MinimumDuration,
            ReleaseAuthority = ReleaseAuthority,
            PrimaryPostService = PrimaryPostService,
            SpotifyId = SpotifyId ?? string.Empty,
            SpotifyMarket = SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = SpotifyEpisodesQueryIsExpensive,
            SpotifyEpisodeCount = SpotifyEpisodeCount,
            AppleId = AppleId,
            AppleEpisodeCount = AppleEpisodeCount,
            YouTubeChannelId = YouTubeChannelId ?? string.Empty,
            YouTubePlaylistId = YouTubePlaylistId ?? string.Empty,
            YouTubePlaylistIdHistory = CloneHistory(YouTubePlaylistIdHistory),
            YouTubePlaylistOrder = YouTubePlaylistOrder,
            YouTubePublicationOffset = YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = YouTubePlaylistQueryIsExpensive,
            YouTubeChannelSearchForbidden = YouTubeChannelSearchForbidden,
            SkipEnrichingFromYouTube = SkipEnrichingFromYouTube,
            TitleRegex = TitleRegex ?? string.Empty,
            DescriptionRegex = DescriptionRegex ?? string.Empty,
            EpisodeMatchRegex = EpisodeMatchRegex ?? string.Empty,
            EpisodeIncludeTitleRegex = EpisodeIncludeTitleRegex ?? string.Empty
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<YouTubePlaylistIdHistoryEntry>? CloneHistory(List<YouTubePlaylistIdHistoryEntry>? history) =>
        history?.Select(entry => new YouTubePlaylistIdHistoryEntry { Id = entry.Id, ReplacedAt = entry.ReplacedAt }).ToList();
}
