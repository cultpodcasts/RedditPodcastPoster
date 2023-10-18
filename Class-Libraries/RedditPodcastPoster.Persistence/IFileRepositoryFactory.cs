namespace RedditPodcastPoster.Persistence;

public interface IFileRepositoryFactory
{
    IFileRepository Create(string container);
    IFileRepository Create();
}