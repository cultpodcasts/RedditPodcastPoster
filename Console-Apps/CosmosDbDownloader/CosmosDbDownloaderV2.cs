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
        await TestFileKeysPerContainer();

        await DownloadPodcasts();
        await DownloadEpisodes();
        await DownloadEliminationTerms();
        await DownloadKnownTerms();
        await DownloadSubjects();
        await DownloadDiscoveryResultsDocuments();
        await DownloadPushSubscriptions();
        await Console.Out.WriteLineAsync("Finished downloading all V2 items.");
        await Console.Out.WriteLineAsync();
    }

    private async Task TestFileKeysPerContainer()
    {
        var progress = new ProgressBar(4);
        var c = 0;
        progress.Refresh(c, "Testing Podcasts");
        await AreUnique(podcastRepository.GetAll().Select(p => p.FileKey), "Podcasts");
        progress.Refresh(++c, "Testing Subjects");
        await AreUnique(subjectRepository.GetAll().Select(s => s.FileKey), "Subjects");
        progress.Refresh(++c, "Testing Discovery Results");
        await AreUnique(discoveryResultsRepository.GetAll().Select(d => d.FileKey), "Discovery Results");
        progress.Refresh(++c, "Testing Push Subscriptions");
        await AreUnique(pushSubscriptionRepository.GetAll().Select(p => p.FileKey), "Push Subscriptions");
        progress.Refresh(++c, "Completed Testing");
    }

    private static async Task AreUnique(IAsyncEnumerable<string> allFileKeys, string name)
    {
        var distinct = new HashSet<string>();
        var duplicate = new HashSet<string>();
        await foreach (var fileKey in allFileKeys)
        {
            if (!distinct.Add(fileKey))
            {
                duplicate.Add(fileKey);
            }
        }

        if (duplicate.Any())
        {
            throw new InvalidOperationException(
                $"Multiple File-keys exist in {name} container: '{string.Join(", ", duplicate)}'.");
        }
    }

    private async Task DownloadPodcasts()
    {
        var count = await podcastRepository.Count();
        var progress = new ProgressBar(count);
        var ctr = 0;
        Directory.CreateDirectory("podcast");
        await foreach (var podcast in podcastRepository.GetAll())
        {
            progress.Refresh(ctr, $"Podcast: {podcast.FileKey}");
            await WriteV2Json("podcast", podcast.FileKey, podcast);
            if (++ctr == count)
            {
                progress.Refresh(ctr, "Finished Podcasts");
            }
        }
    }

    private async Task DownloadEpisodes()
    {
        var count = await episodeRepository.Count();
        var progress = new ProgressBar(count);
        var ctr = 0;
        Directory.CreateDirectory("episode");
        await foreach (var episode in episodeRepository.GetAll())
        {
            progress.Refresh(ctr, $"Episode: {episode.Id}");
            await WriteV2Json("episode", episode.Id.ToString(), episode);
            if (++ctr == count)
            {
                progress.Refresh(ctr, "Finished Episodes");
            }
        }
    }

    private async Task DownloadSubjects()
    {
        var count = await subjectRepository.Count();
        var progress = new ProgressBar(count);
        var ctr = 0;
        await foreach (var subject in subjectRepository.GetAll())
        {
            progress.Refresh(ctr, $"Subject: {subject.FileKey}");
            if (string.IsNullOrWhiteSpace(subject.FileKey))
            {
                logger.LogInformation("Subject with id '{SubjectId}' missing a file-key.", subject.Id);
                subject.FileKey = GetFileKey(subject.Name);
                await subjectRepository.Save(subject);
            }

            await fileWriter.Write(subject);
            if (++ctr == count)
            {
                progress.Refresh(ctr, "Finished Subjects");
            }
        }
    }

    private async Task DownloadDiscoveryResultsDocuments()
    {
        var count = await discoveryResultsRepository.Count();
        var progress = new ProgressBar(count);
        var ctr = 0;
        await foreach (var document in discoveryResultsRepository.GetAll())
        {
            progress.Refresh(ctr, $"DiscoveryResult: {document.FileKey}");
            if (string.IsNullOrWhiteSpace(document.FileKey))
            {
                logger.LogInformation(
                    "Discovery-Results-Document with id '{Guid}' missing a file-key.", document.Id);
                document.FileKey = GetFileKey("dr " + document.Id);
                await discoveryResultsRepository.Save(document);
            }

            await fileWriter.Write(document);
            if (++ctr == count)
            {
                progress.Refresh(ctr, "Finished Discovery Results Documents");
            }
        }
    }

    private async Task DownloadPushSubscriptions()
    {
        var count = await pushSubscriptionRepository.Count();
        var progress = new ProgressBar(count);
        var ctr = 0;
        await foreach (var subscription in pushSubscriptionRepository.GetAll())
        {
            progress.Refresh(ctr, $"PushSubscription: {subscription.FileKey}");
            if (string.IsNullOrWhiteSpace(subscription.FileKey))
            {
                logger.LogInformation(
                    "Push-Subscription-Document with id '{Guid}' missing a file-key.", subscription.Id);
                subscription.FileKey = GetFileKey("ps_" + subscription.Id);
                await pushSubscriptionRepository.Save(subscription);
            }

            await fileWriter.Write(subscription);
            if (++ctr == count)
            {
                progress.Refresh(ctr, "Finished Push Subscriptions");
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