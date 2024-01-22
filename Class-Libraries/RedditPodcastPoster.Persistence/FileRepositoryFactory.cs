using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class FileRepositoryFactory(
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    ILogger<IFileRepository> fileRepositoryLogger,
    ILogger<FileRepositoryFactory> logger)
    : IFileRepositoryFactory
{
    public IFileRepository Create(string container)
    {
        return new FileRepository(
            jsonSerializerOptionsProvider,
            container, 
            fileRepositoryLogger);
    }

    public IFileRepository Create()
    {
        return Create("");
    }
}