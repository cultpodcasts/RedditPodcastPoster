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
        var partitionKey = new Podcast().GetPartitionKey();
        var podcastIds =
            await _cosmosDbRepository.GetAllIds<Podcast>(partitionKey);
        foreach (var podcastId in podcastIds)
        {
            var podcast = await _cosmosDbRepository.Read<Podcast>(podcastId.ToString(), partitionKey);
            await _fileRepository.Write(podcast.FileKey, podcast);
        }

        partitionKey = new EliminationTerms().GetPartitionKey();
        var eliminationTerms =
            await _cosmosDbRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString(), partitionKey);
        await _fileRepository.Write(partitionKey, eliminationTerms);

        partitionKey = new KnownTerms()!.GetPartitionKey();
        var knownTerms =
            await _cosmosDbRepository.Read<KnownTerms>(KnownTerms._Id.ToString(), partitionKey);
        await _fileRepository.Write(partitionKey, knownTerms);
    }
}