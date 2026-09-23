namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Marks a <see cref="StreamingService"/> as retired from submit/prepare/scrape.
/// Enum member and Cosmos wire key remain for historical episode URLs; the service
/// is excluded from the streaming-submit contract and submit DI registrations.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class StreamingServiceSubmitRetiredAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
