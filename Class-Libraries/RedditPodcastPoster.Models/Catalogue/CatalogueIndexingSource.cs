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
public sealed class CatalogueIndexingSource : IYouTubeIndexingSettings
{
    private string? _spotifyId;
    private string? _youTubeChannelId;
    private string? _youTubePlaylistId;
    private string? _youTubePlaylistOwnerChannelId;
    private string? _titleRegex;
    private string? _descriptionRegex;
    private string? _episodeMatchRegex;
    private string? _episodeIncludeTitleRegex;
    private List<AdditionalYouTubeChannel>? _additionalYouTubeChannels;

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

    /// <summary>
    /// Curator flag: a playable the indexer or a URL submit creates under this parent is promoted to the homepage
    /// hero list (same rule as <see cref="Podcast.AlwaysPromoteAsHero"/>, via the hero auto-promote selector).
    /// </summary>
    [JsonPropertyName("alwaysPromoteAsHero")]
    public bool? AlwaysPromoteAsHero { get; set; }

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

    /// <summary>
    /// Channel that owns <see cref="YouTubePlaylistId"/> (playlists.list at playlist save). Used to skip the
    /// uploads-head hint when the parent channel does not own the playlist (#1183).
    /// </summary>
    [JsonPropertyName("youTubePlaylistOwnerChannelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? YouTubePlaylistOwnerChannelId
    {
        get => _youTubePlaylistOwnerChannelId;
        set => _youTubePlaylistOwnerChannelId = NullIfBlank(value);
    }

    /// <summary>
    /// News organisation only (#1370): further YouTube channels the organisation owns and indexes beside
    /// <see cref="YouTubeChannelId"/>, for example an outlet's main channel and its archive channel. Each one is a whole
    /// channel, or is scoped to its own optional <see cref="AdditionalYouTubeChannel.YouTubePlaylistId"/> on that channel
    /// (<see cref="YouTubePlaylistId"/> only ever scopes the main channel). The hourly indexer discovers every one into
    /// the same organisation, and URL submission, discovery and the curator uniqueness / channel-wide claim checks treat
    /// each one as they treat the main channel and its playlist (<see cref="YouTubePlaylistIdOn"/>). Set by the news
    /// organisation PATCH only; the indexer writes back each channel's own expensive-query / search-forbidden flags.
    /// Entries with a blank channel id are dropped and a repeated channel keeps its first entry; an empty list is stored
    /// as null (omitted). Not a podcast field, so <see cref="FromPodcast"/> and <see cref="ToTransientPodcast"/> never
    /// carry it.
    /// </summary>
    [JsonPropertyName("additionalYouTubeChannels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AdditionalYouTubeChannel>? AdditionalYouTubeChannels
    {
        get => _additionalYouTubeChannels;
        set => _additionalYouTubeChannels = NormaliseChannels(value);
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

    /// <summary>Indexer-owned hints for large YouTube playlists (<c>indexing.youTubeIndexingHints</c>). Omitted when null.</summary>
    [JsonPropertyName("youTubeIndexingHints")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public YouTubeIndexingHints? YouTubeIndexingHints { get; set; }

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

    /// <summary>
    /// The main <see cref="YouTubeChannelId"/> (when set) followed by each of <see cref="AdditionalYouTubeChannels"/>,
    /// without repeats: every YouTube channel this parent claims.
    /// </summary>
    public IReadOnlyList<string> AllYouTubeChannelIds()
    {
        var all = new List<string>();
        if (YouTubeChannelId is { } main)
        {
            all.Add(main);
        }

        foreach (var additional in AdditionalYouTubeChannels ?? [])
        {
            if (!all.Contains(additional.YouTubeChannelId, StringComparer.Ordinal))
            {
                all.Add(additional.YouTubeChannelId);
            }
        }

        return all;
    }

    /// <summary>True when <paramref name="channelId"/> is the main or an additional YouTube channel of this parent.</summary>
    public bool HoldsYouTubeChannel(string? channelId) =>
        !string.IsNullOrWhiteSpace(channelId) &&
        AllYouTubeChannelIds().Contains(channelId.Trim(), StringComparer.Ordinal);

    /// <summary>The ids of <see cref="AdditionalYouTubeChannels"/>, in order (empty when none).</summary>
    public IReadOnlyList<string> AdditionalYouTubeChannelIdList() =>
        AdditionalYouTubeChannels?.Select(x => x.YouTubeChannelId).ToList() ?? [];

    /// <summary>The additional-channel entry for <paramref name="channelId"/>, or null when it is not one.</summary>
    public AdditionalYouTubeChannel? AdditionalYouTubeChannelFor(string? channelId) =>
        string.IsNullOrWhiteSpace(channelId)
            ? null
            : AdditionalYouTubeChannels?.FirstOrDefault(x =>
                string.Equals(x.YouTubeChannelId, channelId.Trim(), StringComparison.Ordinal));

    /// <summary>True when this parent holds <paramref name="channelId"/> as one of its additional channels.</summary>
    public bool HoldsAdditionalYouTubeChannel(string? channelId) => AdditionalYouTubeChannelFor(channelId) != null;

    /// <summary>
    /// The playlist that scopes this parent on <paramref name="channelId"/>: that additional channel's own optional
    /// playlist when it is one of <see cref="AdditionalYouTubeChannels"/>, <see cref="YouTubePlaylistId"/> for the main
    /// channel, and null (whole channel, or a channel the parent does not hold) otherwise. The main channel's playlist
    /// never scopes an additional channel.
    /// </summary>
    public string? YouTubePlaylistIdOn(string? channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return null;
        }

        if (AdditionalYouTubeChannelFor(channelId) is { } additional)
        {
            return additional.YouTubePlaylistId;
        }

        return string.Equals(YouTubeChannelId, channelId.Trim(), StringComparison.Ordinal)
            ? YouTubePlaylistId
            : null;
    }

    /// <summary>True when this parent claims the whole of <paramref name="channelId"/> (held, and no playlist scopes it there).</summary>
    public bool ClaimsYouTubeChannelWide(string? channelId) =>
        HoldsYouTubeChannel(channelId) && string.IsNullOrWhiteSpace(YouTubePlaylistIdOn(channelId));

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
        AlwaysPromoteAsHero = podcast.AlwaysPromoteAsHero;
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
        YouTubePlaylistOwnerChannelId = podcast.YouTubePlaylistOwnerChannelId;
        YouTubePlaylistIdHistory = CloneHistory(podcast.YouTubePlaylistIdHistory);
        YouTubePlaylistOrder = podcast.YouTubePlaylistOrder;
        YouTubePublicationOffset = podcast.YouTubePublicationOffset;
        YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive;
        YouTubeIndexingHints = podcast.YouTubeIndexingHints?.Copy();
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
    /// The stored flags are used as they are (no kind default on read): a news organisation is saved with
    /// <see cref="BypassShortEpisodeChecking"/> and <see cref="AlwaysPromoteAsHero"/> on when it is created
    /// (<see cref="ApplyNewsOrganisationCreationDefaults"/>), and a curator may switch either off later.
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
            AlwaysPromoteAsHero = AlwaysPromoteAsHero,
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
            YouTubePlaylistOwnerChannelId = YouTubePlaylistOwnerChannelId,
            YouTubePlaylistIdHistory = CloneHistory(YouTubePlaylistIdHistory),
            YouTubePlaylistOrder = YouTubePlaylistOrder,
            YouTubePublicationOffset = YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = YouTubePlaylistQueryIsExpensive,
            YouTubeIndexingHints = YouTubeIndexingHints?.Copy(),
            YouTubeChannelSearchForbidden = YouTubeChannelSearchForbidden,
            SkipEnrichingFromYouTube = SkipEnrichingFromYouTube,
            TitleRegex = TitleRegex ?? string.Empty,
            DescriptionRegex = DescriptionRegex ?? string.Empty,
            EpisodeMatchRegex = EpisodeMatchRegex ?? string.Empty,
            EpisodeIncludeTitleRegex = EpisodeIncludeTitleRegex ?? string.Empty,
            // News/TV reuse short titles; do not inherit the zero-delay 14-day Spotify/Apple window (#1161).
            CatalogueParentSameTitleMatching = true
        };
    }

    /// <summary>
    /// The transient podcast the indexer runs for one of <see cref="AdditionalYouTubeChannels"/> (#1370): the
    /// <see cref="ToTransientPodcast"/> of this parent with <paramref name="channelId"/> as its only source, as a whole
    /// channel or through that channel's own optional playlist (owned by the channel, so
    /// <see cref="Podcast.YouTubePlaylistOwnerChannelId"/> is the channel). That channel's own expensive-query and
    /// search-forbidden flags are carried, so a refusal or an expensive playlist measured on an earlier pass is honoured.
    /// The main channel's playlist, playlist history, order and hints, its flags, and the Spotify / Apple ids are not
    /// carried, so the pass is a YouTube-only discovery and the main channel's indexer state is never read or moved by
    /// it. The discovery, filter and duration rules are the parent's. A channel that is not one of
    /// <see cref="AdditionalYouTubeChannels"/> is read as a whole channel with no flags.
    /// </summary>
    public Podcast ToTransientPodcastForAdditionalYouTubeChannel(Publisher parent, string channelId)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        var channel = AdditionalYouTubeChannelFor(channelId);
        var podcast = ToTransientPodcast(parent);
        podcast.YouTubeChannelId = channelId.Trim();
        podcast.YouTubePlaylistId = channel?.YouTubePlaylistId ?? string.Empty;
        podcast.YouTubePlaylistOwnerChannelId = channel?.YouTubePlaylistId is null ? null : podcast.YouTubeChannelId;
        podcast.YouTubePlaylistIdHistory = null;
        podcast.YouTubePlaylistOrder = null;
        podcast.YouTubePlaylistQueryIsExpensive = channel?.YouTubePlaylistQueryIsExpensive;
        podcast.YouTubeIndexingHints = null;
        podcast.YouTubeChannelSearchForbidden = channel?.YouTubeChannelSearchForbidden;
        podcast.SkipEnrichingFromYouTube = null;
        podcast.SpotifyId = string.Empty;
        podcast.SpotifyEpisodesQueryIsExpensive = null;
        podcast.SpotifyEpisodeCount = null;
        podcast.AppleId = null;
        podcast.AppleEpisodeCount = null;
        return podcast;
    }

    /// <summary>
    /// Saved defaults for a news organisation when it is created or a podcast (or other parent) is transferred to
    /// one: <see cref="BypassShortEpisodeChecking"/> and <see cref="AlwaysPromoteAsHero"/> are stored as true
    /// (news clips are often short, and new reports go to the homepage hero list), even when the source had them
    /// off or unset. A curator may switch either off afterwards and that saved false is kept. Not applied when a
    /// playable moves into an existing news organisation, and never to a TV show.
    /// </summary>
    public static CatalogueIndexingSource ApplyNewsOrganisationCreationDefaults(CatalogueIndexingSource? indexing)
    {
        var result = indexing ?? new CatalogueIndexingSource();
        result.BypassShortEpisodeChecking = true;
        result.AlwaysPromoteAsHero = true;
        return result;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<AdditionalYouTubeChannel>? NormaliseChannels(IEnumerable<AdditionalYouTubeChannel?>? values)
    {
        if (values is null)
        {
            return null;
        }

        var normalised = new List<AdditionalYouTubeChannel>();
        foreach (var value in values)
        {
            if (value is null || string.IsNullOrWhiteSpace(value.YouTubeChannelId))
            {
                continue;
            }

            if (normalised.All(x => !string.Equals(x.YouTubeChannelId, value.YouTubeChannelId, StringComparison.Ordinal)))
            {
                normalised.Add(value);
            }
        }

        return normalised.Count == 0 ? null : normalised;
    }

    private static List<YouTubePlaylistIdHistoryEntry>? CloneHistory(List<YouTubePlaylistIdHistoryEntry>? history) =>
        history?.Select(entry => new YouTubePlaylistIdHistoryEntry { Id = entry.Id, ReplacedAt = entry.ReplacedAt }).ToList();
}
