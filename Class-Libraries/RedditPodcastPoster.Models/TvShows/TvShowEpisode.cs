using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.TvShows;

/// <summary>
/// Playable TV episode. Platform presence is <see cref="Playable.Services"/> only —
/// no Spotify/Apple/YouTube provider-id fields (those exist on podcast episodes for collection matching).
/// Release is a calendar date (no time-of-day).
/// </summary>
[CosmosSelector(ModelType.TvShowEpisode)]
public sealed class TvShowEpisode : Playable
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
