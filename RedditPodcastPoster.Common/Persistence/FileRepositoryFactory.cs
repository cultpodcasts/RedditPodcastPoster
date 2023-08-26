using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.Persistence;

public class FileRepositoryFactory : IFileRepositoryFactory
{
    private readonly IFilenameSelector _filenameSelector;
    private readonly ILogger<IFileRepository> _fileRepositoryLogger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger<FileRepositoryFactory> _logger;

    public FileRepositoryFactory(
        JsonSerializerOptions jsonSerializerOptions,
        IFilenameSelector filenameSelector,
        ILogger<IFileRepository> fileRepositoryLogger,
        ILogger<FileRepositoryFactory> logger)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
        _filenameSelector = filenameSelector;
        _fileRepositoryLogger = fileRepositoryLogger;
        _logger = logger;
    }

    public IFileRepository Create(string container)
    {
        return new FileRepository(_jsonSerializerOptions, _filenameSelector, container, _fileRepositoryLogger);
    }
}