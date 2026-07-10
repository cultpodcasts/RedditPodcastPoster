using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.ContentPublisher;

public class PeoplePublisher(
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    IPersonRepository personRepository,
    ILogger<PeoplePublisher> logger) : IPeoplePublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;

    public async Task PublishPeople()
    {
        var people = await personRepository.GetAll()
            .OrderBy(x => x.GetEffectiveSortKey())
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                sortName = x.SortName,
                aliases = x.Aliases,
                twitterHandle = x.TwitterHandle,
                blueskyHandle = x.BlueskyHandle
            })
            .ToListAsync();

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.PeopleKey,
            ContentBody = JsonSerializer.Serialize(people, JsonSerializerOptions),
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'.", nameof(PublishPeople));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to upload people-content to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishPeople), _contentOptions.BucketName, _contentOptions.PeopleKey);
        }
    }
}
