using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.TvShows;

[CosmosSelector(ModelType.TvShow)]
public sealed class TvShow : Publisher, ITvCanonical
{
    public TvShow()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.TvShow;
    }

    public TvShow(string name) : this()
    {
        Name = name;
        FileKey = FileKeyFactory.GetTvShowFileKey(name);
    }

    /// <summary>IMDb title page for this programme (homonym disambiguation).</summary>
    [JsonPropertyName("imdb")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Imdb { get; set; }

    /// <summary>IMDb title id (<c>tt…</c>) for this programme. Omitted when unknown.</summary>
    [JsonPropertyName("imdbId")]
    [JsonPropertyOrder(143)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImdbId { get; set; }

    /// <summary>TheTVDB series page for this programme (homonym disambiguation).</summary>
    [JsonPropertyName("tvdb")]
    [JsonPropertyOrder(141)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Tvdb { get; set; }

    /// <summary>TheTVDB series id. Omitted when unknown.</summary>
    [JsonPropertyName("tvdbId")]
    [JsonPropertyOrder(144)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TvdbId { get; set; }

    /// <summary>TMDB series id. Omitted when unknown.</summary>
    [JsonPropertyName("tmdbId")]
    [JsonPropertyOrder(142)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TmdbId { get; set; }
}
