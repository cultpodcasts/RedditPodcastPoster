using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.CosmosDbDownloader;

public class CosmosDbFixer
{
    private readonly CosmosClient _cosmosClient;
    private readonly ICosmosDbRepository _cosmosDbRepository;
    private readonly ILogger<CosmosDbRepository> _logger;

    public CosmosDbFixer(ICosmosDbRepository cosmosDbRepository,
        CosmosClient cosmosClient,
        ILogger<CosmosDbRepository> logger)
    {
        _cosmosDbRepository = cosmosDbRepository;
        _cosmosClient = cosmosClient;
        _logger = logger;
    }

    public async Task Run()
    {
        var podcasts = await _cosmosDbRepository.GetAll<Podcast>().ToListAsync();
        foreach (var podcast in podcasts)
        {
            foreach (var episode in podcast.Episodes)
            {
                episode.ModelType = ModelType.Episode;
            }

            var key = _cosmosDbRepository.KeySelector.GetKey(podcast);
            await _cosmosDbRepository.Write(key, podcast);
        }
    }
}