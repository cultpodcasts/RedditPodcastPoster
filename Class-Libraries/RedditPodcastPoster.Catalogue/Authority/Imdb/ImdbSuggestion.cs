namespace RedditPodcastPoster.Catalogue.Authority.Imdb;

/// <summary>One IMDb title suggestion.</summary>
/// <param name="ImdbId">IMDb title id, <c>tt…</c>.</param>
/// <param name="Title">IMDb display title (often the English title, not the one searched for).</param>
/// <param name="Type">IMDb <c>qid</c>: <c>movie</c>, <c>tvMovie</c>, <c>short</c>, <c>tvSeries</c>, …</param>
/// <param name="Year">Release year when IMDb gives one.</param>
/// <param name="ImageUrl">Poster image when IMDb gives one.</param>
/// <param name="Cast">Principal credits as IMDb lists them.</param>
public sealed record ImdbSuggestion(
    string ImdbId,
    string Title,
    string? Type,
    int? Year,
    Uri? ImageUrl,
    string? Cast)
{
    public const string MovieType = "movie";
    public const string TvMovieType = "tvMovie";

    /// <summary>A feature or TV film. Shorts, series, episodes, and games are not films.</summary>
    public bool IsFilm =>
        string.Equals(Type, MovieType, StringComparison.Ordinal) ||
        string.Equals(Type, TvMovieType, StringComparison.Ordinal);
}
