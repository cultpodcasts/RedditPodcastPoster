using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Things;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Flair = RedditPodcastPoster.ContentPublisher.Models.Flair;
using SubredditSettings = RedditPodcastPoster.Reddit.SubredditSettings;

namespace RedditPodcastPoster.ContentPublisher;

public class ContentPublisher(
    IQueryExecutor queryExecutor,
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ISubjectRepository subjectRepository,
    RedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<ContentPublisher> logger)
    : IContentPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task<PublishHomepageResult> PublishHomepage()
    {
        var homepageContent = await queryExecutor.GetHomePage(CancellationToken.None);
        var homepagePublished = await PublishHomepageToR2(homepageContent);
        //var preProcessedHomepagePublished = await PublishPreProcessedHomepageToR2(homepageContent);
        return new PublishHomepageResult(homepagePublished /*, preProcessedHomepagePublished */);
    }

    public async Task PublishSubjects()
    {
        var subjects = await subjectRepository.GetAll(x => new { name = x.Name }).OrderBy(x => x.name).ToListAsync();
        var json = JsonSerializer.Serialize(subjects, JsonSerializerOptions);

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.SubjectsKey,
            ContentBody = json,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishSubjects)}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method} - Failed to upload subject-content to R2. BucketName: '{bucketName}', Key: '{key}'.",
                nameof(PublishSubjects), _contentOptions.BucketName, _contentOptions.SubjectsKey);
        }
    }

    public async Task PublishFlairs()
    {
        var subredditFlairs = redditClient.Subreddit(_subredditSettings.SubredditName).Flairs.LinkFlairV2;
        var models = subredditFlairs.ToDictionary(x => Guid.Parse(x.Id), ToFlairModel);
        var json = JsonSerializer.Serialize(models, JsonSerializerOptions);
        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.FlairsKey,
            ContentBody = json,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishFlairs)}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method} - Failed to upload flairs-content to R2. BucketName: '{bucketName}', Key: '{key}'.",
                nameof(PublishFlairs), _contentOptions.BucketName, _contentOptions.FlairsKey);
        }
    }

    public async Task PublishDiscoveryInfo(DiscoveryInfo discoveryInfo)
    {
        var json = JsonSerializer.Serialize(discoveryInfo, JsonSerializerOptions);
        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.DiscoveryInfoKey,
            ContentBody = json,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishDiscoveryInfo)}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method} - Failed to upload discovery-info-content to R2. BucketName: '{bucketName}', Key: '{key}', content: '{json}'.",
                nameof(PublishDiscoveryInfo), _contentOptions.BucketName, _contentOptions.DiscoveryInfoKey, json);
        }
    }


    public async Task PublishLanguages()
    {
        var languageCodes = new[]
        {
            "fr",
            "es",
            "de",
            "pt",
            "tr",
            "nl",
            "it",
            "ja",
            "zh",
            "ko",
            "hi",
            "ru",
            "he",
            "ar",
            "bn",
            "id",
            "fil",
            "ur",
            "sw",
            "sk",
            "cs",
            "te",
            "af",
            "fa",
            "ms",
            "no",
            "pa",
            "th",
            "uk"
        };
        var languages = languageCodes.Select(CultureInfo.GetCultureInfo).ToArray();

        var json = JsonSerializer.Serialize(
            languages.Distinct().OrderBy(x => x.EnglishName)
                .ToDictionary(x => x.TwoLetterISOLanguageName, x => x.EnglishName),
            JsonSerializerOptions);
        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.LanguagesKey,
            ContentBody = json,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishLanguages)}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method} - Failed to upload languages-content to R2. BucketName: '{bucketName}', Key: '{key}'.",
                nameof(PublishLanguages), _contentOptions.BucketName, _contentOptions.LanguagesKey);
        }
    }


    private async Task<bool> PublishHomepageToR2(HomePageModel homepageContent)
    {
        var published = false;
        var homepageContentAsJson = JsonSerializer.Serialize(homepageContent, JsonSerializerOptions);

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.HomepageKey,
            ContentBody = homepageContentAsJson,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishHomepage)}'.");
            published = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method} - Failed to upload homepage-content to R2. BucketName: '{bucketName}', Key: '{key}'.",
                nameof(PublishHomepageToR2), _contentOptions.BucketName, _contentOptions.HomepageKey);
        }

        return published;
    }

    private async Task<bool> PublishPreProcessedHomepageToR2(HomePageModel homepageContent)
    {
        var published = false;

        var london = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        const int homepageItems = 20;
        var episodesByDay = homepageContent.RecentEpisodes
            .OrderByDescending(x => x.Release)
            .Take(homepageItems)
            .GroupBy(x => TimeZoneInfo
                .ConvertTime(x.Release, TimeZoneInfo.Utc, london)
                .ToString("dddd d MMMM"))
            .ToDictionary(
                x => x.Key,
                y => y
                    .OrderByDescending(z => z.Release)
                    .ToArray());
        PreProcessedHomePageModel preProcessedHomepage = new()
        {
            TotalDurationDays = homepageContent.TotalDuration.Days,
            HasNext = homepageContent.RecentEpisodes.Count() > homepageItems,
            EpisodesByDay = episodesByDay,
            EpisodesThisWeek = homepageContent.RecentEpisodes.Count(),
            EpisodeCount = homepageContent.EpisodeCount
        };

        var preProcessedHomepageContentAsJson = JsonSerializer.Serialize(preProcessedHomepage, JsonSerializerOptions);

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.PreProcessedHomepageKey,
            ContentBody = preProcessedHomepageContentAsJson,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishPreProcessedHomepageToR2)}'.");
            published = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method} - Failed to upload flairs-content to R2. BucketName: '{bucketName}', Key: '{key}'.",
                nameof(PublishPreProcessedHomepageToR2), _contentOptions.BucketName,
                _contentOptions.PreProcessedHomepageKey);
        }

        return published;
    }

    private Flair ToFlairModel(FlairV2 flair)
    {
        var flairModel = new Flair();
        flairModel.Text = flair.Text;
        flairModel.TextEditable = flair.TextEditable;
        flairModel.TextColour = flair.TextColor;
        flairModel.BackgroundColour = flair.BackgroundColor;
        return flairModel;
    }
}