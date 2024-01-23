using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.KnownTerms;

namespace CosmosDbDownloader;

public class CosmosDbDownloader(
    IFileRepository fileRepository,
    ICosmosDbRepository cosmosDbRepository,
    ILogger<CosmosDbRepository> logger)
{
    public async Task Run()
    {
        var partitionKey = Podcast.PartitionKey;
        var podcastIds =
            await cosmosDbRepository.GetAllIds<Podcast>(partitionKey);
        foreach (var podcastId in podcastIds)
        {
            var podcast = await cosmosDbRepository.Read<Podcast>(podcastId.ToString(), partitionKey);
            if (podcast != null)
            {
                await fileRepository.Write(podcast);
            }
        }

        partitionKey = new EliminationTerms().GetPartitionKey();
        var eliminationTerms =
            await cosmosDbRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString(), partitionKey);
        if (eliminationTerms != null)
        {
            await fileRepository.Write(eliminationTerms);
        }

        partitionKey = new KnownTerms()!.GetPartitionKey();
        var knownTerms =
            await cosmosDbRepository.Read<KnownTerms>(KnownTerms._Id.ToString(), partitionKey);
        if (knownTerms != null)
        {
            await fileRepository.Write(knownTerms);
        }

        partitionKey = Subject.PartitionKey;
        var subjectIds =
            await cosmosDbRepository.GetAllIds<Subject>(partitionKey);
        foreach (var subjectId in subjectIds)
        {
            var subject = await cosmosDbRepository.Read<Subject>(subjectId.ToString(), partitionKey);
            if (subject != null)
            {
                if (string.IsNullOrWhiteSpace(subject.FileKey))
                {
                    logger.LogInformation($"Subject with id '{subject.Id}' missing a file-key.");
                    subject.FileKey = FileKeyFactory.GetFileKey(subject.Name);
                    await cosmosDbRepository.Write(subject);
                }

                await fileRepository.Write(subject);
            }
        }
    }
}