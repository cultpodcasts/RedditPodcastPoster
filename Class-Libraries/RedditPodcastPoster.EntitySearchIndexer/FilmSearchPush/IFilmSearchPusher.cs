namespace RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

/// <summary>
/// Pushes film search documents from the Cosmos films container into the shared search index by Cosmos <c>_ts</c>,
/// in place of a Film pull indexer (the Free tier allows only three data sources: episodes, TV and news).
/// Host-agnostic, so a scheduled host could call it later; today only the FilmSearchPusher console does.
/// </summary>
/// <remarks>
/// DO NOT REMOVE: with the FilmSearchPusher console and the <c>FilmSearchPushWatermark</c> store, this is the only bulk
/// path that gets films into search (the search tier's data sources are all used by episodes, TV and news, so films
/// have no pull indexer), for example after an index rebuild. Do not remove until the D1 search migration (search-platform epic) is online and films are indexed another way.
/// See <c>.cursor/rules/film-search-pusher-keep.mdc</c>.
/// </remarks>
public interface IFilmSearchPusher
{
    Task<FilmSearchPushResult> Push(FilmSearchPushRequest request, CancellationToken cancellationToken);
}
