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
        var podcasts = await fileRepository.GetAll<Podcast>().ToListAsync();
        foreach (var podcast in podcasts)
        {
            await cosmosDbRepository.Write(podcast);
        }

        var eliminationTerms = await fileRepository.GetAll<EliminationTerms>().ToListAsync();
        foreach (var eliminationTermsDocument in eliminationTerms)
        {
            await cosmosDbRepository.Write(eliminationTermsDocument);
        }

        var knownTerms = await fileRepository.GetAll<KnownTerms>().ToListAsync();
        foreach (var knownTermsDocument in knownTerms)
        {
            await cosmosDbRepository.Write(knownTermsDocument);
        }

        var subjects = await fileRepository.GetAll<Subject>().ToListAsync();
        foreach (var subject in subjects)
        {
            await cosmosDbRepository.Write(subject);
        }

        var pushSubscriptions = await fileRepository.GetAll<PushSubscription>().ToListAsync();
        foreach (var pushSubscription in pushSubscriptions)
        {
            await cosmosDbRepository.Write(pushSubscription);
        }

        var discoveryResultsDocuments = await fileRepository.GetAll<DiscoveryResultsDocument>().ToListAsync();
        foreach (var discoveryResultsDocument in discoveryResultsDocuments)
        {
            await cosmosDbRepository.Write(discoveryResultsDocument);
        }
    }
}