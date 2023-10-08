using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.KnownTerms;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;

namespace CosmosDbDownloader;

public class CosmosDbDownloader
{
    private readonly ICosmosDbRepository _cosmosDbRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<CosmosDbRepository> _logger;

    public CosmosDbDownloader(IFileRepository fileRepository,
        ICosmosDbRepository cosmosDbRepository,
        ILogger<CosmosDbRepository> logger)
    {
        _fileRepository = fileRepository;
        _cosmosDbRepository = cosmosDbRepository;
        _logger = logger;
    }

    public async Task Run()
    {
        var partitionKey = _cosmosDbRepository.PartitionKeySelector.GetKey(new Podcast());
        var podcastIds =
            await _cosmosDbRepository.GetAllIds<Podcast>(partitionKey);
        foreach (var podcastId in podcastIds)
        {
            var podcast = await _cosmosDbRepository.Read<Podcast>(podcastId.ToString(), partitionKey);
            await _fileRepository.Write(podcast.FileKey, podcast);
        }

        var eliminationTerms =
            await _cosmosDbRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString(),
                _cosmosDbRepository.PartitionKeySelector.GetKey(new EliminationTerms()));
        partitionKey = _fileRepository.PartitionKeySelector.GetKey(eliminationTerms!);
        await _fileRepository.Write(partitionKey, eliminationTerms);

        var knownTerms =
            await _cosmosDbRepository.Read<KnownTerms>(KnownTerms._Id.ToString(),
                _cosmosDbRepository.PartitionKeySelector.GetKey(new KnownTerms()));
        partitionKey = _fileRepository.PartitionKeySelector.GetKey(knownTerms!);
        await _fileRepository.Write(partitionKey, knownTerms);
    }
}