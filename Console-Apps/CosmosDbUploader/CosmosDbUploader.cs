﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Text.KnownTerms;

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
        throw new NotImplementedException(
            "Changes made to the IFileRepository are untested. Do not use this app until correct behaviour verified.");
        var podcasts = await _fileRepository.GetAll<Podcast>(Podcast.PartitionKey).ToListAsync();
        foreach (var podcast in podcasts)
        {
            await _cosmosDbRepository.Write(podcast.GetPartitionKey(), podcast);
        }

        var eliminationTerms = await _fileRepository.GetAll<EliminationTerms>(EliminationTerms.PartitionKey)
            .ToListAsync();
        foreach (var eliminationTermsDocument in eliminationTerms)
        {
            await _cosmosDbRepository.Write(eliminationTermsDocument.GetPartitionKey(), eliminationTermsDocument);
        }

        var knownTerms = await _fileRepository.GetAll<KnownTerms>(KnownTerms.PartitionKey).ToListAsync();
        foreach (var knownTermsDocument in knownTerms)
        {
            await _cosmosDbRepository.Write(knownTermsDocument.GetPartitionKey(), knownTermsDocument);
        }
    }
}