using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>The catalogue lookup strategies the planner can chain (after a direct id lookup in the resolver).</summary>
public enum CatalogueLookupStrategy
{
    /// <summary>Read every page of a small show (<see cref="EpisodeLookupOptions.FullShowReadMaxEpisodes"/>).</summary>
    FullShowRead,

    /// <summary>Read newest-first pages until past <see cref="CatalogueLookupStep.StopAt"/> or the page cap.</summary>
    NewestPages,

    /// <summary>One platform search by episode title, scoped to the show.</summary>
    TitleSearch,

    /// <summary>Last resort: continue newest-first pages (already-read pages are reused) to a short page cap.</summary>
    BoundedWalk
}

/// <summary>
/// One planned step. <see cref="StopAt"/> bounds page reads by date; <see cref="PageCap"/> bounds them by count;
/// <see cref="Budget"/> (full reads outside Index) bounds reading plus classification by time, after which the
/// executor moves on to the next step.
/// </summary>
public sealed record CatalogueLookupStep(
    CatalogueLookupStrategy Strategy,
    DateTime? StopAt = null,
    int? PageCap = null,
    TimeSpan? Budget = null)
{
    /// <summary>Stable log value (App Insights queries filter on it).</summary>
    public string Stage => Strategy switch
    {
        CatalogueLookupStrategy.FullShowRead => "full-show-read",
        CatalogueLookupStrategy.NewestPages => "newest-pages",
        CatalogueLookupStrategy.TitleSearch => "title-search",
        CatalogueLookupStrategy.BoundedWalk => "bounded-walk",
        _ => Strategy.ToString()
    };
}
