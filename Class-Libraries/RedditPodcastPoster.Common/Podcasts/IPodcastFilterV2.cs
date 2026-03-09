using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

/// <summary>
/// V2 version of IPodcastFilter that works with detached episodes via IEpisodeRepository.
/// Filters episodes based on elimination terms and updates their removed status.
/// </summary>
public interface IPodcastFilterV2
{
    /// <summary>
    /// Filters episodes for a podcast based on elimination terms.
    /// Episodes matching elimination terms will be marked as removed.
    /// </summary>
    /// <param name="podcastId">The ID of the podcast to filter</param>
    /// <param name="eliminationTerms">List of terms that should result in episode removal</param>
    /// <returns>Result containing the filtered episodes</returns>
    Task<FilterResult> Filter(Guid podcastId, List<string> eliminationTerms);
}
