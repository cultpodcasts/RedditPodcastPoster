using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class FileRepositoryFactory : IFileRepositoryFactory
{
    private readonly ILogger<IFileRepository> _fileRepositoryLogger;
    private readonly IJsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly ILogger<FileRepositoryFactory> _logger;

    public FileRepositoryFactory(
        IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        ILogger<IFileRepository> fileRepositoryLogger,
        ILogger<FileRepositoryFactory> logger)
    {
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider;
        _fileRepositoryLogger = fileRepositoryLogger;
        _logger = logger;
    }

    public IFileRepository Create(string container)
    {
        return new FileRepository(
            _jsonSerializerOptionsProvider,
            container, 
            _fileRepositoryLogger);
    }

    public IFileRepository Create()
    {
        return Create("");
    }
}