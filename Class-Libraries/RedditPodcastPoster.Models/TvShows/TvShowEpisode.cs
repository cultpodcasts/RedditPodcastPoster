using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.TvShows;

/// <summary>
/// Playable TV episode. Platform presence is <see cref="Playable.Services"/> (URLs/images).
/// Compact <see cref="YouTubeId"/> is synced from <c>services.youtube.url</c> for search;
/// no Spotify/Apple id members (podcast episodes use nested <c>ids</c> for collection matching).
/// Release is a calendar date (no time-of-day).
/// </summary>
[CosmosSelector(ModelType.TvShowEpisode)]
public sealed class TvShowEpisode : Playable, ICatalogueYouTubeItem, ITvCanonical
{
    public TvShowEpisode()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.TvShowEpisode;
    }

    public TvShowEpisode(string title) : this()
    {
        Title = title;
    }

    [JsonPropertyName("tvShowId")]
    [JsonPropertyOrder(3)]
    public Guid TvShowId { get; set; }

    [JsonPropertyName("tvShowName")]
    [JsonPropertyOrder(90)]
    public string? TvShowName { get; set; }

    /// <summary>IMDb title id (<c>tt…</c>) for this episode. Omitted when unknown.</summary>
    [JsonPropertyName("imdbId")]
    [JsonPropertyOrder(143)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImdbId { get; set; }

    /// <summary>TheTVDB episode id. Omitted when unknown.</summary>
    [JsonPropertyName("tvdbId")]
    [JsonPropertyOrder(144)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TvdbId { get; set; }

    /// <summary>
    /// TMDB episode id. Omitted when unknown.
    /// Refreshing the episode from TMDB still needs the series id plus season and episode number.
    /// </summary>
    [JsonPropertyName("tmdbId")]
    [JsonPropertyOrder(142)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TmdbId { get; set; }

    private string? _youTubeId;

    /// <summary>
    /// Compact YouTube video id. Synced from <c>services.youtube.url</c> on catalogue edits
    /// so Azure Search can project <c>youtubeId</c>. No Spotify/Apple id members.
    /// </summary>
    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(152)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? YouTubeId
    {
        get => _youTubeId;
        set => _youTubeId = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Denormalise parent TvShow fields onto this playable (mirrors
    /// <c>Episode.SetPodcastProperties</c>). First flag = projection fields;
    /// second = parent <c>_ts</c> / metadata version only.
    /// </summary>
    public (bool Updated, bool UpdatedMetadata) SetTvShowProperties(TvShow tvShow)
    {
        var updated = false;
        if (TvShowId != tvShow.Id)
        {
            TvShowId = tvShow.Id;
            updated = true;
        }

        var name = tvShow.Name.Trim();
        if (TvShowName != name)
        {
            TvShowName = name;
            updated = true;
        }

        if (ParentRemoved != tvShow.Removed)
        {
            ParentRemoved = tvShow.Removed;
            updated = true;
        }

        var searchTerms = tvShow.SearchTerms?.Trim();
        if (PublisherSearchTerms != searchTerms)
        {
            PublisherSearchTerms = searchTerms;
            updated = true;
        }

        var language = tvShow.Language?.Trim();
        if (PublisherLanguage != language)
        {
            PublisherLanguage = language;
            updated = true;
        }

        var updatedMetadata = false;
        if (ParentMetadataVersion != tvShow.Timestamp)
        {
            ParentMetadataVersion = tvShow.Timestamp;
            updatedMetadata = true;
        }

        return (updated, updatedMetadata);
    }
}
