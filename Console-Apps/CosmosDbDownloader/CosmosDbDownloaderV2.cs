using System.Text.Json;
using Konsole;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.KnownTerms;
using static RedditPodcastPoster.Models.FileKeyFactory;

namespace CosmosDbDownloader;

public class CosmosDbDownloaderV2(
    ISafeFileEntityWriter fileWriter,
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ISubjectRepositoryV2 subjectRepository,
    ILookupRepositoryV2 lookupRepository,
    IDiscoveryResultsRepositoryV2 discoveryResultsRepository,
    IPushSubscriptionRepositoryV2 pushSubscriptionRepository,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    ILogger<CosmosDbDownloaderV2> logger)
{
    private const string FileExtension = ".json";

    private readonly JsonSerializerOptions _jsonOptions = jsonSerializerOptionsProvider.GetJsonSerializerOptions();

    public async Task Run()
    {
        var podcastFileKeys = await podcastRepository.GetAll()
            .Select(p => p.FileKey)
            .ToListAsync();
        var subjectFileKeys = await subjectRepository.GetAll()
            .Select(s => s.FileKey)
            .ToListAsync();
        var discoveryFileKeys = await discoveryResultsRepository.GetAll()
            .Select(d => d.FileKey)
            .ToListAsync();
        var pushSubscriptionFileKeys = await pushSubscriptionRepository.GetAll()
            .Select(p => p.FileKey)
            .ToListAsync();

        var allFileKeys = podcastFileKeys
            .Union(subjectFileKeys)
            .Union(discoveryFileKeys)
            .Union(pushSubscriptionFileKeys);

        var multipleFileKeys = allFileKeys
            .GroupBy(x => x)
            .Select(x => new { FileKey = x.Key, Count = x.Count() })
            .Where(x => x.Count > 1)
            .Select(x => x.FileKey)
            .ToArray();

        if (multipleFileKeys.Any())
        {
            throw new InvalidOperationException(
                $"Multiple File-keys exist: '{string.Join(", ", multipleFileKeys)}'.");
        }

        await DownloadPodcasts();
        await DownloadEpisodes();
        await DownloadEliminationTerms();
        await DownloadKnownTerms();
        await DownloadSubjects();
        await DownloadDiscoveryResultsDocuments();
        await DownloadPushSubscriptions();
    }

    private async Task DownloadPodcasts()
    {
        var podcasts = await podcastRepository.GetAll().ToListAsync();
        var progress = new ProgressBar(podcasts.Count);
        var ctr = 0;
        Directory.CreateDirectory("podcast");
        foreach (var podcast in podcasts)
        {
            progress.Refresh(ctr, $"Downloaded {podcast.FileKey}");
            await WriteV2Json("podcast", podcast.FileKey, podcast);
            if (++ctr == podcasts.Count)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }

    private async Task DownloadEpisodes()
    {
        var episodes = await episodeRepository.GetAllBy(_ => true).ToListAsync();
        var progress = new ProgressBar(episodes.Count);
        var ctr = 0;
        Directory.CreateDirectory("episode");
        foreach (var episode in episodes)
        {
            progress.Refresh(ctr, $"Downloaded {episode.Id}");
            await WriteV2Json("episode", episode.Id.ToString(), episode);
            if (++ctr == episodes.Count)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }

    private async Task DownloadSubjects()
    {
        var subjects = await subjectRepository.GetAll().ToListAsync();
        var progress = new ProgressBar(subjects.Count);
        var ctr = 0;
        foreach (var subject in subjects)
        {
            progress.Refresh(ctr, $"Downloaded {subject.FileKey}");
            if (string.IsNullOrWhiteSpace(subject.FileKey))
            {
                logger.LogInformation("Subject with id '{SubjectId}' missing a file-key.", subject.Id);
                subject.FileKey = GetFileKey(subject.Name);
                await subjectRepository.Save(subject);
            }

            await fileWriter.Write(subject);
            if (++ctr == subjects.Count)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }

    private async Task DownloadDiscoveryResultsDocuments()
    {
        var documents = await discoveryResultsRepository.GetAll().ToListAsync();
        var progress = new ProgressBar(documents.Count);
        var ctr = 0;
        foreach (var document in documents)
        {
            progress.Refresh(ctr, $"Downloaded {document.FileKey}");
            if (string.IsNullOrWhiteSpace(document.FileKey))
            {
                logger.LogInformation(
                    "Discovery-Results-Document with id '{Guid}' missing a file-key.", document.Id);
                document.FileKey = GetFileKey("dr " + document.Id);
                await discoveryResultsRepository.Save(document);
            }

            await fileWriter.Write(document);
            if (++ctr == documents.Count)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }

    private async Task DownloadPushSubscriptions()
    {
        var subscriptions = await pushSubscriptionRepository.GetAll().ToListAsync();
        var progress = new ProgressBar(subscriptions.Count);
        var ctr = 0;
        foreach (var subscription in subscriptions)
        {
            progress.Refresh(ctr, $"Downloaded {subscription.FileKey}");
            if (string.IsNullOrWhiteSpace(subscription.FileKey))
            {
                logger.LogInformation(
                    "Push-Subscription-Document with id '{Guid}' missing a file-key.", subscription.Id);
                subscription.FileKey = GetFileKey("ps_" + subscription.Id);
                await pushSubscriptionRepository.Save(subscription);
            }

            await fileWriter.Write(subscription);
            if (++ctr == subscriptions.Count)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }

    private async Task DownloadKnownTerms()
    {
        var knownTerms = await lookupRepository.GetKnownTerms<KnownTerms>();
        if (knownTerms != null)
        {
            await fileWriter.Write(knownTerms);
        }
    }

    private async Task DownloadEliminationTerms()
    {
        var eliminationTerms = await lookupRepository.GetEliminationTerms();
        if (eliminationTerms != null)
        {
            await fileWriter.Write(eliminationTerms);
        }
    }

    private async Task WriteV2Json<T>(string folder, string fileName, T item)
    {
        var path = Path.Combine(folder, $"{fileName}{FileExtension}");
        if (File.Exists(path))
        {
            throw new InvalidOperationException(
                $"File '{path}' already exists when writing item '{fileName}'.");
        }

        var json = JsonSerializer.Serialize(item, _jsonOptions);
        await File.WriteAllTextAsync(path, json);
    }
}