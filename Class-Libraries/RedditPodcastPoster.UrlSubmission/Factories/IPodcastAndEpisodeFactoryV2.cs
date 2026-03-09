using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Factories;

/// <summary>
/// V2 version of IPodcastAndEpisodeFactory that creates podcasts and episodes
/// without using embedded episode collections. Episodes are saved to detached repository.
/// </summary>
public interface IPodcastAndEpisodeFactoryV2
{
    /// <summary>
    /// Creates a new podcast with an initial episode, persisting to V2 repositories.
    /// </summary>
    Task<CreatePodcastWithEpisodeResponseV2> CreatePodcastWithEpisode(
        CategorisedItem categorisedItem);
}
