using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.HomePage;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Models.Notifications;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Models.YouTubeQuota;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.KnownTerms;

namespace CosmosDbUploader;

public class CosmosDbUploader(
    IFileRepository fileRepository,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ISubjectRepository subjectRepository,
    ILookupRepository lookupRepository,
    ILanguageTitleCasingRulesRepository titleCasingRulesRepository,
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPushSubscriptionRepository pushSubscriptionRepository,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    ILogger<CosmosDbUploader> logger)
{
    private const string FileExtension = ".json";
    private readonly JsonSerializerOptions _jsonOptions = jsonSerializerOptionsProvider.GetJsonSerializerOptions();

    public async Task Run()
    {
        await UploadPodcasts();
        await UploadEpisodes();
        await UploadLookUps();
        await UploadTitleCasingRules();
        await UploadSubjects();
        await UploadDiscoveryResultsDocuments();
        await UploadPushSubscriptions();
    }

    private async Task UploadPodcasts()
    {
        foreach (var podcast in ReadFiles<Podcast>("podcast"))
        {
            logger.LogInformation("Uploading podcast '{FileKey}'.", podcast.FileKey);
            await podcastRepository.Save(podcast);
        }
    }

    private async Task UploadEpisodes()
    {
        foreach (var episode in ReadFiles<Episode>("episode"))
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

    /// <summary>
    /// Uploads every JSON file under <c>lookups/</c>, dispatching by document <c>type</c>.
    /// Legacy KnownTerms files are still accepted if present.
    /// </summary>
    private async Task UploadLookUps()
    {
        const string folder = "lookups";
        if (!Directory.Exists(folder))
        {
            // Backward compatible: older dumps wrote typed Cosmoselectors via IFileRepository (cwd root).
            await UploadLegacyLookUpSingletons();
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, $"*{FileExtension}"))
        {
            var json = await File.ReadAllTextAsync(file);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("type", out var typeElement))
            {
                throw new InvalidOperationException($"LookUps file '{file}' is missing required 'type' property.");
            }

            var type = typeElement.GetString();
            logger.LogInformation("Uploading lookup '{File}' (type={Type}).", Path.GetFileName(file), type);

            switch (type)
            {
                case nameof(ModelType.EliminationTerms):
                    await lookupRepository.SaveEliminationTerms(
                        DeserializeRequired<EliminationTerms>(json, file));
                    break;
                case nameof(ModelType.DiscoveryScheduleConfig):
                    await lookupRepository.SaveDiscoveryScheduleConfig(
                        DeserializeRequired<DiscoveryScheduleConfig>(json, file));
                    break;
                case nameof(ModelType.SupportedLanguagesConfig):
                    await lookupRepository.SaveSupportedLanguagesConfig(
                        DeserializeRequired<SupportedLanguagesConfig>(json, file));
                    break;
                case nameof(ModelType.KnownTerms):
                    await lookupRepository.SaveKnownTerms(
                        DeserializeRequired<KnownTerms>(json, file));
                    break;
                case nameof(ModelType.HomePageCache):
                    await lookupRepository.SaveHomePageCache(
                        DeserializeRequired<HomePageCache>(json, file));
                    break;
                case nameof(ModelType.YouTubeQuotaReport):
                    await lookupRepository.SaveYouTubeQuotaReport(
                        DeserializeRequired<YouTubeQuotaReport>(json, file));
                    break;
                case nameof(ModelType.YouTubeIndexerKeyState):
                    await lookupRepository.SaveYouTubeIndexerKeyState(
                        DeserializeRequired<YouTubeIndexerKeyState>(json, file));
                    break;
                case nameof(ModelType.YouTubeQuotaUsageState):
                    await lookupRepository.SaveYouTubeQuotaUsageState(
                        DeserializeRequired<YouTubeQuotaUsageState>(json, file));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"LookUps file '{file}' has unsupported type '{type}'.");
            }
        }
    }

    private async Task UploadLegacyLookUpSingletons()
    {
        logger.LogWarning(
            "Directory 'lookups' not found — falling back to legacy singleton file-repository uploads.");

        var eliminationTerms = await fileRepository.GetAll<EliminationTerms>().FirstOrDefaultAsync();
        if (eliminationTerms != null)
        {
            logger.LogInformation("Uploading elimination terms.");
            await lookupRepository.SaveEliminationTerms(eliminationTerms);
        }

        var knownTerms = await fileRepository.GetAll<KnownTerms>().FirstOrDefaultAsync();
        if (knownTerms != null)
        {
            logger.LogInformation("Uploading known terms.");
            await lookupRepository.SaveKnownTerms(knownTerms);
        }

        var supportedLanguages = await fileRepository.GetAll<SupportedLanguagesConfig>().FirstOrDefaultAsync();
        if (supportedLanguages != null)
        {
            logger.LogInformation("Uploading supported languages.");
            await lookupRepository.SaveSupportedLanguagesConfig(supportedLanguages);
        }
    }

    private T DeserializeRequired<T>(string json, string file)
    {
        var item = JsonSerializer.Deserialize<T>(json, _jsonOptions);
        if (item is null)
        {
            throw new InvalidOperationException($"Failed to deserialise LookUps file '{file}' as {typeof(T).Name}.");
        }

        return item;
    }

    private async Task UploadTitleCasingRules()
    {
        foreach (var document in ReadFiles<LanguageTitleCasingRulesDocument>("titlecasing"))
        {
            logger.LogInformation("Uploading title-casing rules '{FileKey}'.", document.FileKey);
            await titleCasingRulesRepository.Save(document);
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

    private IEnumerable<T> ReadFiles<T>(string folder)
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
