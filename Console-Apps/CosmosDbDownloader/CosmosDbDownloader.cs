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
        var podcastIds = cosmosDbRepository.GetAllIds<Podcast>();
        foreach (var podcastId in await podcastIds.ToArrayAsync())
        {
            var podcast = await cosmosDbRepository.Read<Podcast>(podcastId.ToString());
            if (podcast != null)
            {
                await fileRepository.Write(podcast);
            }
        }

        var eliminationTerms =
            await cosmosDbRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString());
        if (eliminationTerms != null)
        {
            await fileRepository.Write(eliminationTerms);
        }

        var knownTerms =
            await cosmosDbRepository.Read<KnownTerms>(KnownTerms._Id.ToString());
        if (knownTerms != null)
        {
            await fileRepository.Write(knownTerms);
        }

        var subjectIds = await cosmosDbRepository.GetAllIds<Subject>().ToArrayAsync();
        foreach (var subjectId in subjectIds)
        {
            var subject = await cosmosDbRepository.Read<Subject>(subjectId.ToString());
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