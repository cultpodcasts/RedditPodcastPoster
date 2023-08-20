using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.CosmosDbUploader;

public class CosmosDbUploader
{
    private readonly CosmosClient _cosmosClient;
    private readonly ICosmosDbRepository _cosmosDbRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<CosmosDbRepository> _logger;

    public CosmosDbUploader(
        IFileRepository fileRepository,
        ICosmosDbRepository cosmosDbRepository,
        CosmosClient cosmosClient,
        ILogger<CosmosDbRepository> logger)
    {
        _fileRepository = fileRepository;
        _cosmosDbRepository = cosmosDbRepository;
        _cosmosClient = cosmosClient;
        _logger = logger;
    }

    public async Task Run()
    {
        var podcasts = await _fileRepository.GetAll<Podcast>().ToListAsync();
        foreach (var podcast in podcasts)
        {
            var key = _cosmosDbRepository.KeySelector.GetKey(podcast);
            await _cosmosDbRepository.Write(key, podcast);
        }
    }
}