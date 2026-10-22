using System.Net;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

/// <summary>
/// Serves canned thumbnail responses by URL so probe rules can replay what YouTube's image CDN
/// returned. An unserved URL answers 404 with an empty body. Every requested URL is recorded.
/// </summary>
public sealed class YouTubeThumbnailHttpStub : HttpMessageHandler
{
    private readonly Dictionary<Uri, (HttpStatusCode Status, byte[] Body)> _responses = new();
    private readonly List<Uri> _requested = [];
    private readonly List<System.Net.Http.Headers.RangeHeaderValue?> _requestedRanges = [];
    private readonly HashSet<Uri> _failing = [];
    private readonly HashSet<Uri> _hanging = [];

    /// <summary>Invoked before each request is answered (e.g. to advance a fake clock).</summary>
    public Action<Uri>? OnRequest { get; set; }

    public IReadOnlyList<Uri> Requested => _requested;

    /// <summary>The Range header sent with each request, in request order (null when none was sent).</summary>
    public IReadOnlyList<System.Net.Http.Headers.RangeHeaderValue?> RequestedRanges => _requestedRanges;

    public YouTubeThumbnailHttpStub Serve(Uri url, HttpStatusCode status, byte[] body)
    {
        _responses[url] = (status, body);
        return this;
    }

    /// <summary>Requests to <paramref name="url"/> fail at the network layer.</summary>
    public YouTubeThumbnailHttpStub FailNetwork(Uri url)
    {
        _failing.Add(url);
        return this;
    }

    /// <summary>Requests to <paramref name="url"/> never answer until cancelled.</summary>
    public YouTubeThumbnailHttpStub Hang(Uri url)
    {
        _hanging.Add(url);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri!;
        _requested.Add(url);
        _requestedRanges.Add(request.Headers.Range);
        OnRequest?.Invoke(url);
        if (_hanging.Contains(url))
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (_failing.Contains(url))
        {
            throw new HttpRequestException($"Network failure for '{url}'.");
        }

        var (status, body) = _responses.TryGetValue(url, out var response)
            ? response
            : (HttpStatusCode.NotFound, []);
        return new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
    }
}
