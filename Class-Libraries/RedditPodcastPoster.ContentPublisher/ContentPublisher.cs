using System.Text.Json;
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
    private readonly ContentOptions _contentOptions = contentOptions.Value;
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task PublishHomepage()
    {
        var homepageContent = await queryExecutor.GetHomePage(CancellationToken.None);
        await PublishHomepageToR2(homepageContent);
        await PublishPreProcessedHomepageToR2(homepageContent);
    }

    public async Task PublishSubjects()
    {
        var subjects = await subjectRepository.GetAll(x => new {name = x.Name}).OrderBy(x => x.name).ToListAsync();
        var json = JsonSerializer.Serialize(subjects);

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
            logger.LogError(ex, $"{nameof(PublishSubjects)} - Failed to upload subjects-content to R2");
        }
    }

    public async Task PublishFlairs()
    {
        var subredditFlairs = redditClient.Subreddit(_subredditSettings.SubredditName).Flairs.LinkFlairV2;
        var models = subredditFlairs.ToDictionary(x => Guid.Parse(x.Id), ToFlairModel);
        var json = JsonSerializer.Serialize(models);
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
            logger.LogError(ex, $"{nameof(PublishFlairs)} - Failed to upload flairs-content to R2");
        }
    }

    public async Task PublishDiscoveryInfo(DiscoveryInfo discoveryInfo)
    {
        var json = JsonSerializer.Serialize(discoveryInfo);
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
            logger.LogError(ex, $"{nameof(PublishDiscoveryInfo)} - Failed to upload discovery-info-content to R2");
        }
    }

    private async Task PublishHomepageToR2(HomePageModel homepageContent)
    {
        var homepageContentAsJson = JsonSerializer.Serialize(homepageContent);

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PublishHomepage)} - Failed to upload homepage-content to R2");
        }
    }

    private async Task PublishPreProcessedHomepageToR2(HomePageModel homepageContent)
    {
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

        var preProcessedHomepageContentAsJson = JsonSerializer.Serialize(preProcessedHomepage);

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PublishPreProcessedHomepageToR2)} - Failed to upload homepage-content to R2");
        }
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