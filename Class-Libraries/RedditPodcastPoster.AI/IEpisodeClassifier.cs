using RedditPodcastPoster.Models.Episodes;

public interface IEpisodeClassifier
{
    Task CategoriseEpisode(Episode episode);
}