using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.KnownTerms;

namespace CosmosDbUploader;

public class CosmosDbUploaderV2(
    IFileRepository fileRepository,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ISubjectRepository subjectRepository,
    ILookupRepository lookupRepository,
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPushSubscriptionRepository pushSubscriptionRepository,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    ILogger<CosmosDbUploaderV2> logger)
{
    private const string FileExtension = ".json";
    private readonly JsonSerializerOptions _jsonOptions = jsonSerializerOptionsProvider.GetJsonSerializerOptions();

    public async Task Run()
    {
        await UploadPodcasts();
        await UploadEpisodes();
        await UploadEliminationTerms();
        await UploadKnownTerms();
        await UploadSubjects();
        await UploadDiscoveryResultsDocuments();
        await UploadPushSubscriptions();
    }

    private async Task UploadPodcasts()
    {
        foreach (var podcast in ReadV2Files<Podcast>("podcast"))
        {
            logger.LogInformation("Uploading podcast '{FileKey}'.", podcast.FileKey);
            await podcastRepository.Save(podcast);
        }
    }

    private async Task UploadEpisodes()
    {
        foreach (var episode in ReadV2Files<Episode>("episode"))
        {
            logger.LogInformation("Uploading episode '{Id}'.", episode.Id);
            await episodeRepository.Save(episode);
        }
    }

    private async Task UploadSubjects()
    {
        await foreach (var subject in fileRepository.GetAll<Subject>())
        {
            logger.LogInformation("Uploading subject '{FileKey}'.", subject.FileKey);
            await subjectRepository.Save(subject);
        }
    }

    private async Task UploadEliminationTerms()
    {
        var eliminationTerms = await fileRepository.GetAll<EliminationTerms>().FirstOrDefaultAsync();
        if (eliminationTerms != null)
        {
            logger.LogInformation("Uploading elimination terms.");
            await lookupRepository.SaveEliminationTerms(eliminationTerms);
        }
    }

    private async Task UploadKnownTerms()
    {
        var knownTerms = await fileRepository.GetAll<KnownTerms>().FirstOrDefaultAsync();
        if (knownTerms != null)
        {
            logger.LogInformation("Uploading known terms.");
            await lookupRepository.SaveKnownTerms(knownTerms);
        }
    }

    private async Task UploadDiscoveryResultsDocuments()
    {
        await foreach (var document in fileRepository.GetAll<DiscoveryResultsDocument>())
        {
            logger.LogInformation("Uploading discovery results document '{FileKey}'.", document.FileKey);
            await discoveryResultsRepository.Save(document);
        }
    }

    private async Task UploadPushSubscriptions()
    {
        await foreach (var subscription in fileRepository.GetAll<PushSubscription>())
        {
            logger.LogInformation("Uploading push subscription '{FileKey}'.", subscription.FileKey);
            await pushSubscriptionRepository.Save(subscription);
        }
    }

    private IEnumerable<T> ReadV2Files<T>(string folder)
    {
        if (!Directory.Exists(folder))
        {
            logger.LogWarning("Directory '{Folder}' not found — skipping.", folder);
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(folder, $"*{FileExtension}"))
        {
            var json = File.ReadAllText(file);
            var item = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            if (item != null)
            {
                yield return item;
            }
            else
            {
                logger.LogWarning("Failed to deserialise '{File}'.", file);
            }
        }
    }
}