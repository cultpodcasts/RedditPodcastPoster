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
    private readonly HashSet<Uri> _failing = [];

    public IReadOnlyList<Uri> Requested => _requested;

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

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri!;
        _requested.Add(url);
        if (_failing.Contains(url))
        {
            throw new HttpRequestException($"Network failure for '{url}'.");
        }

        var (status, body) = _responses.TryGetValue(url, out var response)
            ? response
            : (HttpStatusCode.NotFound, []);
        return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
    }
}
