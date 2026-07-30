using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Builders;
using RedditPodcastPoster.ContentPublisher.Configuration;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public class SearchSuggestionsPublisher(
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ISearchSuggestionsIndexBuilder indexBuilder,
    ILogger<SearchSuggestionsPublisher> logger) : ISearchSuggestionsPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;

    public async Task PublishSearchSuggestions(CancellationToken cancellationToken = default)
    {
        try
        {
            var corpus = await indexBuilder.BuildAsync(cancellationToken);
            var request = new PutObjectRequest
            {
                BucketName = _contentOptions.BucketName,
                Key = _contentOptions.SearchSuggestionsKey,
                ContentBody = JsonSerializer.Serialize(corpus, JsonSerializerOptions),
                ContentType = "application/json",
                DisablePayloadSigning = true
            };

            await client.PutObjectAsync(request, cancellationToken);
            logger.LogInformation(
                "Completed '{MethodName}'. Published {EntryCount} search-suggestion entries to '{Key}'.",
                nameof(PublishSearchSuggestions),
                corpus.Entries.Length,
                _contentOptions.SearchSuggestionsKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to publish search-suggestions to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishSearchSuggestions),
                _contentOptions.BucketName,
                _contentOptions.SearchSuggestionsKey);
            throw;
        }
    }
}
