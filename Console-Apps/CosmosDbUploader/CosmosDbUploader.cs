using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.KnownTerms;

namespace CosmosDbUploader;

public class CosmosDbUploader(
    IFileRepository fileRepository,
    ICosmosDbRepository cosmosDbRepository,
    ILogger<CosmosDbRepository> logger)
{
    private readonly ILogger<CosmosDbRepository> _logger = logger;

    public async Task Run()
    {
        throw new NotImplementedException(
            "Changes made to the IFileRepository are untested. Do not use this app until correct behaviour verified.");
#pragma warning disable CS0162 // Unreachable code detected
        var podcasts = await fileRepository.GetAll<Podcast>(Podcast.PartitionKey).ToListAsync();
        foreach (var podcast in podcasts)
        {
            await cosmosDbRepository.Write(podcast);
        }

        var eliminationTerms = await fileRepository.GetAll<EliminationTerms>(EliminationTerms.PartitionKey)
            .ToListAsync();
        foreach (var eliminationTermsDocument in eliminationTerms)
        {
            await cosmosDbRepository.Write(eliminationTermsDocument);
        }

        var knownTerms = await fileRepository.GetAll<KnownTerms>(KnownTerms.PartitionKey).ToListAsync();
        foreach (var knownTermsDocument in knownTerms)
        {
            await cosmosDbRepository.Write(knownTermsDocument);
        }

        var subjects = await fileRepository.GetAll<Subject>(Subject.PartitionKey).ToListAsync();
        foreach (var subject in subjects)
        {
            await cosmosDbRepository.Write(subject);
        }
#pragma warning restore CS0162 // Unreachable code detected
    }
}