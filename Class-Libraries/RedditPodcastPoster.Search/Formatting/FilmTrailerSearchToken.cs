using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Search.Formatting;

/// <summary>
/// One entry of the film search document's <c>trailers</c> field: <c>{duration}|{url}|{title}</c>.
/// Trailers have no search documents of their own (the index is at the Free-tier quota), so the film document
/// carries just enough for the film page to list them. The title is last, so a <c>|</c> in it needs no escaping:
/// a reader splits on the first two <c>|</c> only. The duration is the search <c>duration</c> format (whole seconds
/// drop the <c>.0000000</c> tail). The push path (<see cref="Format"/>) and the pull path (<see cref="CosmosSql"/>)
/// produce the same text for the same trailer.
/// </summary>
public static class FilmTrailerSearchToken
{
    public const char Separator = '|';

    public static string Format(FilmTrailer trailer)
    {
        ArgumentNullException.ThrowIfNull(trailer);
        var duration = trailer.Length.ToString();
        if (duration.EndsWith(".0000000", StringComparison.Ordinal))
        {
            duration = duration[..^8];
        }

        return $"{duration}{Separator}{trailer.Url.OriginalString}{Separator}{trailer.Title}";
    }

    /// <summary>The film's tokens in display order, or null when it has none (so the field is not sent).</summary>
    public static string[]? For(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        return film.Trailers is { Count: > 0 } trailers ? trailers.Select(Format).ToArray() : null;
    }

    /// <summary>
    /// Cosmos SQL projecting <c>trailers</c> for the film pull indexer. An absent <c>trailers</c> array yields
    /// <c>[]</c>, which clears the field when a film's last trailer was removed.
    /// Cosmos stores the URL as <see cref="Uri.OriginalString"/> and the title as saved (writers trim it), so both
    /// paths agree.
    /// </summary>
    public static string CosmosSql(string documentAlias = "c") =>
        $@"ARRAY(SELECT VALUE CONCAT(IIF(ENDSWITH(t.duration, "".0000000""), SUBSTRING(t.duration, 0, LENGTH(t.duration) - 8), t.duration), ""{Separator}"", t.url, ""{Separator}"", t.title) FROM t IN {documentAlias}.trailers)";
}
