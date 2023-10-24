namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IFileRepositoryFactory
{
    IFileRepository Create(string container);
    IFileRepository Create();
}