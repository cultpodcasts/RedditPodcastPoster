using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Factories;

public interface IPodcastAndEpisodeFactory
{
    Task<CreatePodcastWithEpisodeResponse> CreatePodcastWithEpisode(
        CategorisedItem categorisedItem);
}