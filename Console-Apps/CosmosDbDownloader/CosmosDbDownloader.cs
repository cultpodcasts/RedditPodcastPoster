using Konsole;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.KnownTerms;

namespace CosmosDbDownloader;

public class CosmosDbDownloader(
    ISafeFileEntityWriter fileWriter,
    ICosmosDbRepository cosmosDbRepository,
    ILogger<CosmosDbRepository> logger)
{
    public async Task Run()
    {
        var podcastFileKeys = await cosmosDbRepository.GetAllFileKeys<Podcast>().ToListAsync();
        var subjectFileKeys = await cosmosDbRepository.GetAllFileKeys<Subject>().ToListAsync();
        var eliminationTermsFileKeys = await cosmosDbRepository.GetAllFileKeys<EliminationTerms>().ToListAsync();
        var knownTermsFileKeys = await cosmosDbRepository.GetAllFileKeys<KnownTerms>().ToListAsync();
        var discoveryResultsDocumentFileKeys =
            await cosmosDbRepository.GetAllFileKeys<DiscoveryResultsDocument>().ToListAsync();
        var pushSubscriptionFileKeys = await cosmosDbRepository.GetAllFileKeys<PushSubscription>().ToListAsync();

        var fileKeys = podcastFileKeys
            .Union(subjectFileKeys)
            .Union(eliminationTermsFileKeys)
            .Union(knownTermsFileKeys)
            .Union(discoveryResultsDocumentFileKeys)
            .Union(pushSubscriptionFileKeys);
        var multipleFileKeys = fileKeys
            .GroupBy(x => x)
            .Select(x => new {FileKey = x.Key, Count = x.Count()})
            .Where(x => x.Count > 1)
            .Select(x => x.FileKey)
            .ToArray();
        if (multipleFileKeys.Any())
        {
            throw new InvalidOperationException($"Multiple File-keys exist: '{string.Join(", ", multipleFileKeys)}'.");
        }

        await DownloadPodcasts();
        await DownloadEliminationTerms();
        await DownloadKnownTerms();
        await DownloadSubjects();
        await DownloadDiscoveryResultsDocuments();
        await DownloadPushSubscriptions();
    }

    private async Task DownloadPushSubscriptions()
    {
        var pushSubscriptionIds = await cosmosDbRepository.GetAllIds<PushSubscription>().ToArrayAsync();
        var progress = new ProgressBar(pushSubscriptionIds.Length);
        var ctr = 0;
        foreach (var pushSubscriptionId in pushSubscriptionIds)
        {
            var pushSubscriptionDocument =
                await cosmosDbRepository.Read<PushSubscription>(pushSubscriptionId.ToString());
            if (pushSubscriptionDocument != null)
            {
                progress.Refresh(ctr, $"Downloaded {pushSubscriptionDocument.FileKey}");
                if (string.IsNullOrWhiteSpace(pushSubscriptionDocument.FileKey))
                {
                    logger.LogInformation(
                        $"Push-Subscription-Document with id '{pushSubscriptionDocument.Id}' missing a file-key.");
                    pushSubscriptionDocument.FileKey = FileKeyFactory.GetFileKey("ps_" + pushSubscriptionDocument.Id);
                    await cosmosDbRepository.Write(pushSubscriptionDocument);
                }

                await fileWriter.Write(pushSubscriptionDocument);
            }

            if (++ctr == pushSubscriptionIds.Length)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }

    private async Task DownloadDiscoveryResultsDocuments()
    {
        var documentIds = await cosmosDbRepository.GetAllIds<DiscoveryResultsDocument>().ToArrayAsync();
        var progress = new ProgressBar(documentIds.Length);
        var ctr = 0;
        foreach (var discoveryResultsDocumentId in documentIds)
        {
            var discoveryResultsDocument =
                await cosmosDbRepository.Read<DiscoveryResultsDocument>(discoveryResultsDocumentId.ToString());
            if (discoveryResultsDocument != null)
            {
                progress.Refresh(ctr, $"Downloaded {discoveryResultsDocument.FileKey}");
                if (string.IsNullOrWhiteSpace(discoveryResultsDocument.FileKey))
                {
                    logger.LogInformation(
                        $"Discovery-Results-Document with id '{discoveryResultsDocument.Id}' missing a file-key.");
                    discoveryResultsDocument.FileKey = FileKeyFactory.GetFileKey("dr " + discoveryResultsDocument.Id);
                    await cosmosDbRepository.Write(discoveryResultsDocument);
                }

                await fileWriter.Write(discoveryResultsDocument);
            }

            if (++ctr == documentIds.Length)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }

    private async Task DownloadSubjects()
    {
        var subjectIds = await cosmosDbRepository.GetAllIds<Subject>().ToArrayAsync();
        var progress = new ProgressBar(subjectIds.Length);
        var ctr = 0;
        foreach (var subjectId in subjectIds)
        {
            var subject = await cosmosDbRepository.Read<Subject>(subjectId.ToString());
            if (subject != null)
            {
                progress.Refresh(ctr, $"Downloaded {subject.FileKey}");
                if (string.IsNullOrWhiteSpace(subject.FileKey))
                {
                    logger.LogInformation($"Subject with id '{subject.Id}' missing a file-key.");
                    subject.FileKey = FileKeyFactory.GetFileKey(subject.Name);
                    await cosmosDbRepository.Write(subject);
                }

                await fileWriter.Write(subject);
            }

            if (++ctr == subjectIds.Length)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }

    private async Task DownloadKnownTerms()
    {
        var knownTerms =
            await cosmosDbRepository.Read<KnownTerms>(KnownTerms._Id.ToString());
        if (knownTerms != null)
        {
            await fileWriter.Write(knownTerms);
        }
    }

    private async Task DownloadEliminationTerms()
    {
        var eliminationTerms =
            await cosmosDbRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString());
        if (eliminationTerms != null)
        {
            await fileWriter.Write(eliminationTerms);
        }
    }

    private async Task DownloadPodcasts()
    {
        var ctr = 0;
        var podcastIds = await cosmosDbRepository.GetAllIds<Podcast>().ToArrayAsync();
        var progress = new ProgressBar(podcastIds.Length);
        foreach (var podcastId in podcastIds)
        {
            var podcast = await cosmosDbRepository.Read<Podcast>(podcastId.ToString());
            if (podcast != null)
            {
                progress.Refresh(ctr, $"Downloaded {podcast.FileKey}");
                await fileWriter.Write(podcast);
            }

            if (++ctr == podcastIds.Length)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }
}