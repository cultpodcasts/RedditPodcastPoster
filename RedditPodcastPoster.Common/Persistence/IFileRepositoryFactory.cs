namespace RedditPodcastPoster.Common.Persistence;

public interface IFileRepositoryFactory
{
    IFileRepository Create(string container);
}