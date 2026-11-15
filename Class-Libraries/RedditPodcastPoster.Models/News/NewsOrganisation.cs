using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.News;

[CosmosSelector(ModelType.NewsOrganisation)]
public sealed class NewsOrganisation : Publisher, IHasYouTubeIndexingSettings
{
    private List<AdditionalYouTubeChannel>? _additionalYouTubeChannels;

    public NewsOrganisation()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.NewsOrganisation;
    }

    /// <summary>
    /// Sets <see cref="Name"/> and a provisional, never-empty file key built from this constructor's own new
    /// <c>Id</c>. Setting <c>Id</c> in an object initializer afterwards does not update the key, and the key is not
    /// checked for uniqueness: code that saves a new item must set <c>FileKey</c> from <c>IFileKeyAllocator</c>.
    /// </summary>
    public NewsOrganisation(string name) : this()
    {
        Name = name;
        FileKey = FileKeyFactory.GetNewsOrganisationFileKey(name, Id);
    }

    /// <summary>
    /// Episode-discovery settings kept from the migrated podcast (JSON <c>indexing</c>). When
    /// <see cref="CatalogueIndexingSource.IsAutoIndexed"/>, the hourly indexer finds new playables.
    /// Omitted when unset.
    /// </summary>
    [JsonPropertyName("indexing")]
    [JsonPropertyOrder(300)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CatalogueIndexingSource? Indexing { get; set; }

    /// <summary>
    /// The organisation's further YouTube channels beside its main <see cref="CatalogueIndexingSource.YouTubeChannelId"/>
    /// (#1370, JSON <c>additionalYouTubeChannels</c>), for example an outlet's main channel and its archive channel. An
    /// outlet can publish on several channels; a TV show or a podcast cannot, so this lives on the news organisation and
    /// not on the shared <see cref="CatalogueIndexingSource"/>. Each channel is a whole channel, or is scoped to its own
    /// optional <see cref="AdditionalYouTubeChannel.YouTubePlaylistId"/> on that channel (the main
    /// <see cref="CatalogueIndexingSource.YouTubePlaylistId"/> only ever scopes the main channel). The hourly indexer
    /// discovers every one into this organisation, and URL submission, discovery and the curator uniqueness /
    /// channel-wide claim checks treat each one as they treat the main channel and its playlist
    /// (<see cref="CatalogueParentYouTubeChannels"/>). Set by the news organisation PATCH only; the indexer writes back
    /// each channel's own expensive-query / search-forbidden flags. Entries with a blank channel id are dropped and a
    /// repeated channel keeps its first entry; an empty list is stored as null (omitted), so a document without the
    /// field is an organisation with one channel.
    /// </summary>
    [JsonPropertyName("additionalYouTubeChannels")]
    [JsonPropertyOrder(301)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AdditionalYouTubeChannel>? AdditionalYouTubeChannels
    {
        get => _additionalYouTubeChannels;
        set => _additionalYouTubeChannels = AdditionalYouTubeChannel.Normalise(value);
    }

    /// <summary>The YouTube discovery settings and hints live on <see cref="Indexing"/>.</summary>
    [JsonIgnore]
    public IYouTubeIndexingSettings? YouTubeIndexing => Indexing;

    /// <summary>The ids of <see cref="AdditionalYouTubeChannels"/>, in order (empty when none).</summary>
    public IReadOnlyList<string> AdditionalYouTubeChannelIdList() =>
        AdditionalYouTubeChannels?.Select(x => x.YouTubeChannelId).ToList() ?? [];

    /// <summary>The additional-channel entry for <paramref name="channelId"/>, or null when it is not one.</summary>
    public AdditionalYouTubeChannel? AdditionalYouTubeChannelFor(string? channelId) =>
        string.IsNullOrWhiteSpace(channelId)
            ? null
            : AdditionalYouTubeChannels?.FirstOrDefault(x =>
                string.Equals(x.YouTubeChannelId, channelId.Trim(), StringComparison.Ordinal));

    /// <summary>True when this organisation holds <paramref name="channelId"/> as one of its additional channels.</summary>
    public bool HoldsAdditionalYouTubeChannel(string? channelId) => AdditionalYouTubeChannelFor(channelId) != null;

    /// <summary>
    /// The transient podcast the indexer runs for one of <see cref="AdditionalYouTubeChannels"/> (#1370): the
    /// <see cref="CatalogueIndexingSource.ToTransientPodcast"/> of this organisation with <paramref name="channelId"/> as
    /// its only source, as a whole channel or through that channel's own optional playlist (owned by the channel, so
    /// <see cref="Podcast.YouTubePlaylistOwnerChannelId"/> is the channel). That channel's own expensive-query and
    /// search-forbidden flags are carried, so a refusal or an expensive playlist measured on an earlier pass is honoured.
    /// The main channel's playlist, playlist history, order and hints, its flags, and the Spotify / Apple ids are not
    /// carried, so the pass is a YouTube-only discovery and the main channel's indexer state is never read or moved by
    /// it. The discovery, filter and duration rules are the organisation's. A channel that is not one of
    /// <see cref="AdditionalYouTubeChannels"/> is read as a whole channel with no flags.
    /// </summary>
    public Podcast ToTransientPodcastForAdditionalYouTubeChannel(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        var channel = AdditionalYouTubeChannelFor(channelId);
        var podcast = (Indexing ?? new CatalogueIndexingSource()).ToTransientPodcast(this);
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
}
