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
        var podcast = podcasts.SingleOrDefault(x => x.Id == Guid.Parse("67d44b5c-7421-4f9a-89db-5564f67da7a9"));
        foreach (var episode in podcast!.Episodes)
        {
            episode.Subjects.Clear();
            episode.Subjects.AddRange(new[]
            {
                "LIG",
                "Lighthouse International Group",
                "Paul Waugh",
                "Christopher Nash",
                "Chris Nash",
                "Shaun Cooper",
                "Warren Vaughan",
                "A Very British Cult"
            });
        }

        var key = _cosmosDbRepository.KeySelector.GetKey(podcast);
        await _cosmosDbRepository.Write(key, podcast);
    }
}