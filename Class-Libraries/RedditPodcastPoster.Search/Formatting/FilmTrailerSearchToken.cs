using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Search.Formatting;

/// <summary>
/// One entry of the film search document's <c>trailers</c> field: <c>{duration}|{url}|{title}</c>.
/// Trailers have no search documents of their own (the index is at the Free-tier quota), so the film document
/// carries just enough for the film page to list them. The title is last, so a <c>|</c> in it needs no escaping:
/// a reader splits on the first two <c>|</c> only. A <c>|</c> in the URL is written as <c>%7C</c>, because that
/// same split would otherwise cut the URL. The duration is the search <c>duration</c> format (whole seconds
/// drop the <c>.0000000</c> tail). Films are pushed to search (curator edits, trailer attach and Move to film all
/// build <c>CataloguePlayableSearchDocument.For(Film)</c>); there is no film pull query.
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

        var url = trailer.Url.OriginalString.Replace("|", "%7C", StringComparison.Ordinal);
        return $"{duration}{Separator}{url}{Separator}{trailer.Title}";
    }

    /// <summary>The film's tokens in display order, or null when it has none (so the field is not sent).</summary>
    public static string[]? For(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        return film.Trailers is { Count: > 0 } trailers ? trailers.Select(Format).ToArray() : null;
    }
}
