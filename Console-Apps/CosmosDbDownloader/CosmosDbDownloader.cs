using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.KnownTerms;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.CosmosDbDownloader;

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
        string? key;
        var podcasts = await _cosmosDbRepository.GetAll<Podcast>().ToListAsync();
        foreach (var podcast in podcasts)
        {
            key = _fileRepository.KeySelector.GetKey(podcast);
            await _fileRepository.Write(key, podcast);
        }

        var eliminationTerms =
            await _cosmosDbRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString(),
            _cosmosDbRepository.KeySelector.GetKey(new EliminationTerms()));
        key = _fileRepository.KeySelector.GetKey(eliminationTerms!);
        await _fileRepository.Write(key, eliminationTerms);

        var knownTerms =
            await _cosmosDbRepository.Read<KnownTerms>(KnownTerms._Id.ToString(),
                _cosmosDbRepository.KeySelector.GetKey(new KnownTerms()));
        key = _fileRepository.KeySelector.GetKey(knownTerms!);
        await _fileRepository.Write(key, knownTerms);

    }
}