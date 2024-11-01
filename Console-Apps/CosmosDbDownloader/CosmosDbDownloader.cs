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
        var fileKeys = await cosmosDbRepository.GetAllFileKeys().ToListAsync();
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