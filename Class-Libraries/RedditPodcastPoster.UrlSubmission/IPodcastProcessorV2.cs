using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// V2 version of IPodcastProcessor that adds episodes to existing podcasts
/// using detached IEpisodeRepository instead of mutating podcast.Episodes.
/// </summary>
public interface IPodcastProcessorV2
{
    /// <summary>
    /// Adds an episode to an existing podcast, persisting via detached repository.
    /// </summary>
    Task<SubmitResult> AddEpisodeToExistingPodcast(CategorisedItem categorisedItem);
}
