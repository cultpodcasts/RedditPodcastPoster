namespace RedditPodcastPoster.Catalogue.Authority.Imdb;

/// <summary>
/// IMDb's public title suggestion feed (the search-box autocomplete). It needs no key and
/// matches alternative titles, so a Danish original such as "Faderhuset" finds the film
/// IMDb lists in English as "House of Holy Father".
/// </summary>
public interface IImdbSuggestionClient
{
    /// <summary>
    /// Title suggestions for <paramref name="query"/>. Names and other non-title rows are dropped.
    /// A failed or unreadable response is logged and returns no suggestions, because IMDb is a fallback.
    /// </summary>
    Task<IReadOnlyList<ImdbSuggestion>> SuggestAsync(string query, CancellationToken cancellationToken = default);
}
