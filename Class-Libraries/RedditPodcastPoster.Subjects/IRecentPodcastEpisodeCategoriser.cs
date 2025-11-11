namespace RedditPodcastPoster.Subjects;

public interface IRecentPodcastEpisodeCategoriser
{
    Task<IList<Guid>> Categorise();
}