using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Dtos;

public class SubmitUrlPrepareResponse
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("podcastName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PodcastName { get; init; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? Duration { get; init; }

    [JsonPropertyName("release")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Release { get; init; }

    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Image { get; init; }

    [JsonPropertyName("explicit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Explicit { get; init; }

    [JsonPropertyName("publisher")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Publisher { get; init; }

    [JsonPropertyName("showName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShowName { get; init; }

    public static SubmitUrlPrepareResponse From(
        Uri url,
        NonPodcastServiceItemMetaData meta,
        NonPodcastService service)
    {
        var serviceKey = ServiceCatalog.TryResolveKey(url)
                         ?? throw new InvalidOperationException(
                             $"ServiceCatalog.TryResolveKey returned null for extractable url '{url}'.");

        return new SubmitUrlPrepareResponse
        {
            Service = serviceKey,
            PodcastName = NonPodcastShowNameResolver.TrySeriesName(meta.ShowName, meta.Publisher, service),
            Title = meta.Title,
            Description = meta.Description,
            Duration = meta.Duration,
            Release = meta.Release,
            Image = meta.Image,
            Explicit = meta.Explicit,
            Publisher = meta.Publisher,
            ShowName = meta.ShowName
        };
    }
}
