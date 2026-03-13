using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Things;
using RedditPodcastPoster.Persistence.Abstractions;
using Flair = RedditPodcastPoster.ContentPublisher.Models.Flair;
using SubredditSettings = RedditPodcastPoster.Reddit.SubredditSettings;

namespace RedditPodcastPoster.ContentPublisher;

public class SubjectsPublisher(
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ISubjectRepositoryV2 subjectRepository,
    RedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<SubjectsPublisher> logger) : ISubjectsPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task PublishSubjects()
    {
        var subjects = await subjectRepository.GetAll()
            .Select(x => new { name = x.Name })
            .OrderBy(x => x.name)
            .ToListAsync();

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.SubjectsKey,
            ContentBody = JsonSerializer.Serialize(subjects, JsonSerializerOptions),
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'.", nameof(PublishSubjects));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to upload subject-content to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishSubjects), _contentOptions.BucketName, _contentOptions.SubjectsKey);
        }
    }

    public async Task PublishFlairs()
    {
        var subredditFlairs = redditClient.Subreddit(_subredditSettings.SubredditName).Flairs.LinkFlairV2;
        var models = subredditFlairs.ToDictionary(x => Guid.Parse(x.Id), ToFlairModel);

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.FlairsKey,
            ContentBody = JsonSerializer.Serialize(models, JsonSerializerOptions),
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'.", nameof(PublishFlairs));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to upload flairs-content to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishFlairs), _contentOptions.BucketName, _contentOptions.FlairsKey);
        }
    }

    private static Flair ToFlairModel(FlairV2 flair)
    {
        return new Flair
        {
            Text = flair.Text,
            TextEditable = flair.TextEditable,
            TextColour = flair.TextColor,
            BackgroundColour = flair.BackgroundColor
        };
    }
}
