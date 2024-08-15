using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Things;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Persistence.Abstractions;
using Flair = RedditPodcastPoster.ContentPublisher.Models.Flair;
using SubredditSettings = RedditPodcastPoster.Reddit.SubredditSettings;

namespace RedditPodcastPoster.ContentPublisher;

public class ContentPublisher(
    IQueryExecutor queryExecutor,
    IAmazonS3 client,
    IOptions<CloudFlareOptions> cloudFlareOptions,
    ISubjectRepository subjectRepository,
    RedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<ContentPublisher> logger)
    : IContentPublisher
{
    private readonly CloudFlareOptions _cloudFlareOptions = cloudFlareOptions.Value;
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task PublishHomepage()
    {
        var homepageContent = await queryExecutor.GetHomePage(CancellationToken.None);
        var homepageContentAsJson = JsonSerializer.Serialize(homepageContent);

        var request = new PutObjectRequest
        {
            BucketName = _cloudFlareOptions.BucketName,
            Key = _cloudFlareOptions.HomepageKey,
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

    public async Task PublishSubjects()
    {
        var subjects = await subjectRepository.GetAll(x => new {name = x.Name}).OrderBy(x => x.name).ToListAsync();
        var json = JsonSerializer.Serialize(subjects);

        var request = new PutObjectRequest
        {
            BucketName = _cloudFlareOptions.BucketName,
            Key = _cloudFlareOptions.SubjectsKey,
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
            BucketName = _cloudFlareOptions.BucketName,
            Key = _cloudFlareOptions.FlairsKey,
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