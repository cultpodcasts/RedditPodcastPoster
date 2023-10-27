using RedditPodcastPoster.Models;

public interface IEpisodeClassifier
{
    Task CategoriseEpisode(Episode episode);
}