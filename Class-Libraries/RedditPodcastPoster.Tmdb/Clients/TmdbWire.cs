using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Tmdb.Clients;

internal sealed class TmdbSearchResponse
{
    public List<TmdbListedTitleDto>? Results { get; set; }
}

internal sealed class TmdbFindResponse
{
    [JsonPropertyName("movie_results")]
    public List<TmdbListedTitleDto>? MovieResults { get; set; }

    [JsonPropertyName("tv_episode_results")]
    public List<TmdbFoundEpisodeDto>? TvEpisodeResults { get; set; }
}

internal sealed class TmdbFoundEpisodeDto
{
    public int Id { get; set; }

    [JsonPropertyName("show_id")]
    public int ShowId { get; set; }

    [JsonPropertyName("season_number")]
    public int? SeasonNumber { get; set; }

    [JsonPropertyName("episode_number")]
    public int? EpisodeNumber { get; set; }
}

/// <summary>Movie details with <c>append_to_response=alternative_titles,translations</c>.</summary>
internal sealed class TmdbMovieMetadataDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    [JsonPropertyName("original_title")]
    public string? OriginalTitle { get; set; }

    public string? Overview { get; set; }

    public int? Runtime { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string? BackdropPath { get; set; }

    [JsonPropertyName("alternative_titles")]
    public TmdbAlternativeTitlesDto? AlternativeTitles { get; set; }

    public TmdbTranslationsDto? Translations { get; set; }
}

internal sealed class TmdbAlternativeTitlesDto
{
    public List<TmdbAlternativeTitleDto>? Titles { get; set; }
}

internal sealed class TmdbAlternativeTitleDto
{
    public string? Title { get; set; }
}

internal sealed class TmdbTranslationsDto
{
    public List<TmdbTranslationDto>? Translations { get; set; }
}

internal sealed class TmdbTranslationDto
{
    public TmdbTranslationDataDto? Data { get; set; }
}

internal sealed class TmdbTranslationDataDto
{
    public string? Title { get; set; }

    public string? Name { get; set; }
}

/// <summary>TV episode details: <c>tv/{series}/season/{n}/episode/{n}</c>.</summary>
internal sealed class TmdbEpisodeMetadataDto
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Overview { get; set; }

    public int? Runtime { get; set; }

    [JsonPropertyName("air_date")]
    public string? AirDate { get; set; }

    [JsonPropertyName("still_path")]
    public string? StillPath { get; set; }
}

internal sealed class TmdbListedTitleDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("original_title")]
    public string? OriginalTitle { get; set; }

    [JsonPropertyName("original_name")]
    public string? OriginalName { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    public string? Overview { get; set; }
}

internal sealed class TmdbDetailsDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; set; }

    [JsonPropertyName("air_date")]
    public string? AirDate { get; set; }

    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("season_number")]
    public int? SeasonNumber { get; set; }

    [JsonPropertyName("episode_number")]
    public int? EpisodeNumber { get; set; }

    [JsonPropertyName("external_ids")]
    public TmdbExternalIdsDto? ExternalIds { get; set; }
}

internal sealed class TmdbExternalIdsDto
{
    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tvdb_id")]
    public long? TvdbId { get; set; }
}
