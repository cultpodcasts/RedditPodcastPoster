using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.AI.Classifiers;

public interface IEpisodeClassifier
{
    Task CategoriseEpisode(Episode episode);
}
