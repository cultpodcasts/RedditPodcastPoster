using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class FileRepositoryFactory(
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    ILogger<IFileRepository> fileRepositoryLogger,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<FileRepositoryFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IFileRepositoryFactory
{
    public IFileRepository Create(string container, bool useEntityFolder)
    {
        return new FileRepository(
            jsonSerializerOptionsProvider,
            container,
            useEntityFolder,
            fileRepositoryLogger);
    }

    public IFileRepository Create()
    {
        return Create("", false);
    }
}