using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;

/// <summary>A synthetic page the fake YouTube API serves for one page token.</summary>
public sealed record FakeYouTubePage(IReadOnlyList<string> VideoIds, string? NextPageToken, int? TotalResults = null)
{
    /// <summary>Newest-first publish times, one minute apart, ending at <paramref name="newest"/>.</summary>
    public DateTimeOffset? Newest { get; init; }

    /// <summary>Explicit per-item publish times (overrides <see cref="Newest"/>); a null entry has no date.</summary>
    public IReadOnlyList<DateTimeOffset?>? Dates { get; init; }
}

/// <summary>
/// Fake YouTube Data API: serves synthetic playlistItems.list / search.list pages keyed by
/// <c>pageToken</c> and records each request's <c>pageToken</c> and <c>maxResults</c>.
/// Requests past <see cref="MaxRequests"/> fail so a looping walk ends the test instead of hanging it.
/// </summary>
public sealed class FakeYouTubePagingApi
{
    public const int MaxRequests = 60;

    private readonly Dictionary<string, FakeYouTubePage> _pages = new(StringComparer.Ordinal);

    public List<(string PageToken, int? MaxResults)> Requests { get; } = [];

    public FakeYouTubePagingApi Page(string pageToken, FakeYouTubePage page)
    {
        _pages[pageToken] = page;
        return this;
    }

    public YouTubeService CreateService() =>
        new(new BaseClientService.Initializer
        {
            HttpClientFactory = new FakeHttpClientFactory(new Handler(this)),
            ApiKey = "synthetic-test-key"
        });

    public static IReadOnlyList<string> Ids(string prefix, int from, int count) =>
        Enumerable.Range(from, count).Select(i => $"{prefix}{i:D4}").ToList();

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var token = query["pageToken"] ?? "";
        int? maxResults = int.TryParse(query["maxResults"], out var m) ? m : null;
        Requests.Add((token, maxResults));
        if (Requests.Count > MaxRequests || !_pages.TryGetValue(token, out var page))
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{ \"error\": { \"code\": 400, \"message\": \"synthetic: no page\" } }")
            };
        }

        var isSearch = request.RequestUri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal);
        var newest = page.Newest ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var items = page.VideoIds.Select((id, i) =>
        {
            var date = page.Dates != null ? page.Dates[i] : newest.AddMinutes(-i);
            var published = date?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            var snippet = new Dictionary<string, object?>
            {
                ["title"] = $"Synthetic {id}",
                ["resourceId"] = new { kind = "youtube#video", videoId = id }
            };
            var contentDetails = new Dictionary<string, object?> { ["videoId"] = id };
            if (published != null)
            {
                snippet["publishedAt"] = published;
                contentDetails["videoPublishedAt"] = published;
            }

            return isSearch
                ? (object) new
                {
                    kind = "youtube#searchResult",
                    id = new { kind = "youtube#video", videoId = id },
                    snippet = new { title = $"Synthetic {id}", publishedAt = published, liveBroadcastContent = "none" }
                }
                : new
                {
                    kind = "youtube#playlistItem",
                    id = $"item-{id}",
                    snippet,
                    contentDetails
                };
        }).ToList();
        var body = new Dictionary<string, object?>
        {
            ["kind"] = isSearch ? "youtube#searchListResponse" : "youtube#playlistItemListResponse",
            ["items"] = items
        };
        if (page.NextPageToken != null)
        {
            body["nextPageToken"] = page.NextPageToken;
        }

        if (page.TotalResults.HasValue)
        {
            body["pageInfo"] = new { totalResults = page.TotalResults, resultsPerPage = maxResults };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
    }

    private sealed class Handler(FakeYouTubePagingApi api) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(api.Respond(request));
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : Google.Apis.Http.IHttpClientFactory
    {
        public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args) =>
            new(new Google.Apis.Http.ConfigurableMessageHandler(handler));
    }
}
