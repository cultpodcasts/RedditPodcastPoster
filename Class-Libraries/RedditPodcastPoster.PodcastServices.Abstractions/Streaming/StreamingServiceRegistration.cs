using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

public sealed class StreamingServiceRegistration : IStreamingServiceRegistration
{
    private readonly Func<Uri, string?>? _tryResolve;
    private readonly Func<Uri, string?>? _tryCompact;
    private readonly Func<string, Uri?>? _tryExpand;

    public StreamingServiceRegistration(
        StreamingService service,
        Func<Uri, string?>? tryResolve = null,
        Func<Uri, string?>? tryCompact = null,
        Func<string, Uri?>? tryExpand = null)
    {
        Service = service;
        Descriptor = StreamingServiceWire.ToDescriptor(service);
        _tryResolve = tryResolve;
        _tryCompact = tryCompact;
        _tryExpand = tryExpand;
    }

    public StreamingService Service { get; }

    public ServiceCatalog.Descriptor Descriptor { get; }

    public string? TryResolveKey(Uri url)
    {
        if (_tryResolve is not null)
        {
            return _tryResolve(url);
        }

        if (!url.IsAbsoluteUri)
        {
            return null;
        }

        var host = ServiceCatalog.CanonicalHost(url);
        return Descriptor.Hosts.Any(h => ServiceCatalog.IsHost(host, h)) ? Descriptor.Key : null;
    }

    public string? TryCompactUrl(Uri url) => _tryCompact?.Invoke(url);

    public Uri? TryExpandCompactUrl(string payload) => _tryExpand?.Invoke(payload);
}
