namespace RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

/// <summary>
/// Pushes film search documents from the Cosmos films container into the shared search index by Cosmos <c>_ts</c>,
/// in place of a Film pull indexer (the Free tier allows only three data sources: episodes, TV and news).
/// Host-agnostic, so a scheduled host could call it later; today only the FilmSearchPusher console does.
/// </summary>
public interface IFilmSearchPusher
{
    Task<FilmSearchPushResult> Push(FilmSearchPushRequest request, CancellationToken cancellationToken);
}
