using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.Persistence;

public class FileRepositoryFactory : IFileRepositoryFactory
{
    private readonly IPartitionKeySelector _partitionKeySelector;
    private readonly ILogger<IFileRepository> _fileRepositoryLogger;
    private readonly IJsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly ILogger<FileRepositoryFactory> _logger;

    public FileRepositoryFactory(
        IPartitionKeySelector partitionKeySelector,
        IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        ILogger<IFileRepository> fileRepositoryLogger,
        ILogger<FileRepositoryFactory> logger)
    {
        _partitionKeySelector = partitionKeySelector;
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider;
        _fileRepositoryLogger = fileRepositoryLogger;
        _logger = logger;
    }

    public IFileRepository Create(string container)
    {
        var jsonSerializerOptions = _jsonSerializerOptionsProvider.GetJsonSerializerOptions();
        jsonSerializerOptions.WriteIndented = true;
        return new FileRepository(
            jsonSerializerOptions,
            _partitionKeySelector,
            container, _fileRepositoryLogger);
    }

    public IFileRepository Create()
    {
        return Create("");
    }
}