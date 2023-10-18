using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence;

public class RepositoryFactory : IRepositoryFactory
{
    private readonly IFileRepositoryFactory _fileRepositoryFactory;
    private readonly ILogger<RepositoryFactory> _logger;
    private readonly ILogger<Repository<CosmosSelector>> _repositoryLogger;

    public RepositoryFactory(
        IFileRepositoryFactory fileRepositoryFactory,
        ILogger<RepositoryFactory> logger, 
        ILogger<Repository<CosmosSelector>> repositoryLogger)
    {
        _fileRepositoryFactory = fileRepositoryFactory;
        _logger = logger;
        _repositoryLogger = repositoryLogger;
    }

    public IRepository<T> Create<T>(string container) where T : CosmosSelector
    {
        var repository = new Repository<T>(_fileRepositoryFactory.Create(container), _repositoryLogger);
        return repository;
    }
}