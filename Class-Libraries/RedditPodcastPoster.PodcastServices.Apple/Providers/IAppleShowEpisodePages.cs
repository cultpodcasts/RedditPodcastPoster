using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

/// <summary>
/// amp-api episode pages of one Apple show, read in order (page i needs page i-1's <c>next</c>) and each fetched at
/// most once per podcast pass. Catalogues Apple serves oldest-first are flagged so date early-stop is off.
/// </summary>
public interface IAppleShowEpisodePages
{
    Task<CatalogueShowPage<AppleEpisode>?> GetNewestPage(long podcastId, int pageIndex, CancellationToken cancellationToken = default);
}
