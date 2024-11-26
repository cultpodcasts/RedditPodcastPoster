namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IFileRepositoryFactory
{
    IFileRepository Create(string container, bool useEntityFolder);
    IFileRepository Create();
}