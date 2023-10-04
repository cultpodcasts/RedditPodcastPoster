using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.Persistence;

public class FileRepositoryFactory : IFileRepositoryFactory
{
    private readonly IFilenameSelector _filenameSelector;
    private readonly ILogger<IFileRepository> _fileRepositoryLogger;
    private readonly IJsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly ILogger<FileRepositoryFactory> _logger;

    public FileRepositoryFactory(
        IFilenameSelector filenameSelector,
        IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        ILogger<IFileRepository> fileRepositoryLogger,
        ILogger<FileRepositoryFactory> logger)
    {
        _filenameSelector = filenameSelector;
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
            _filenameSelector,
            container, _fileRepositoryLogger);
    }
}