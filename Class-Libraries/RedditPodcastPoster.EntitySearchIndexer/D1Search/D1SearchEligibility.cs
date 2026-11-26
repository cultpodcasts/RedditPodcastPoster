using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// Which playables the D1 index holds: exactly the ones Azure holds, so the soak compares like with like.
/// <list type="bullet">
/// <item>Podcast episode: <see cref="EpisodeSearchIndexEligibility"/> (episode or live podcast removed), plus the
/// denormalised <c>parentRemoved</c> that the Azure episode pull filters on (<c>ActiveEpisodesFilter</c>).</item>
/// <item>TV episode / news report: <see cref="CataloguePlayableSearchIndexEligibility"/>, the sibling pull's rule.</item>
/// <item>Film: removed films are out.</item>
/// </list>
/// An excluded playable is deleted from D1 (a no-op <c>notFound</c> when it was never there).
/// </summary>
public static class D1SearchEligibility
{
    public static bool ShouldExclude(Podcast podcast, Episode episode) =>
        EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode) || episode.ParentRemoved == true;

    public static bool ShouldExclude(Playable playable) =>
        CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(playable);

    public static bool ShouldExclude(Film film) =>
        CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(film);
}
