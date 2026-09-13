using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

/// <summary>
/// One streaming destination: catalog row, host/path resolve, and optional compact search payload.
/// Provider assemblies register an instance; Models does not switch on hosts or URL grammar.
/// </summary>
public interface IStreamingServiceRegistration
{
    ServiceCatalog.Descriptor Descriptor { get; }

    string? TryResolveKey(Uri url);

    string? TryCompactUrl(Uri url);

    Uri? TryExpandCompactUrl(string payload);
}
