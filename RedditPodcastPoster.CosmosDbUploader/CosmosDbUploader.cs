using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.CosmosDbUploader;

public class CosmosDbUploader
{
    private readonly ICosmosDbRepository _cosmosDbRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<CosmosDbRepository> _logger;

    public CosmosDbUploader(
        IFileRepository fileRepository,
        ICosmosDbRepository cosmosDbRepository,
        ILogger<CosmosDbRepository> logger)
    {
        _fileRepository = fileRepository;
        _cosmosDbRepository = cosmosDbRepository;
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
        var eliminationTerms = await _fileRepository.GetAll<EliminationTerms>().ToListAsync();
        foreach (var eliminationTermsDocument in eliminationTerms)
        {
            var key = _cosmosDbRepository.KeySelector.GetKey(eliminationTermsDocument);
            await _cosmosDbRepository.Write(key, eliminationTermsDocument);
        }
    }
}