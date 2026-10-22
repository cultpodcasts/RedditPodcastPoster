using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.IndexingHints;

/// <summary>A synthetic video in a fake playlist.</summary>
public sealed record FakeYouTubeVideo(string Id, DateTime PublishedUtc);

/// <summary>
/// Fake YouTube Data API for the indexing hints: serves synthetic playlistItems.list responses by <c>playlistId</c>
/// (paged by offset tokens, with <c>pageInfo.totalResults</c>) and answers the <c>videoId</c>-filtered membership
/// check. Every request is recorded, and every successful call is counted as quota units through
/// <see cref="QuotaTracker"/> by the services under test (playlistItems.list is 1 unit per call).
/// </summary>
public sealed class FakeYouTubeHintsApi
{
    private const int MaxRequests = 60;
    private readonly Dictionary<string, List<FakeYouTubeVideo>> _playlists = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _totals = new(StringComparer.Ordinal);

    public FakeYouTubeHintsApi()
    {
        QuotaTracker
            .Setup(x => x.RecordQuotaConsumedAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(),
                It.IsAny<YouTubeQuotaOperation>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Application, ApplicationUsage, YouTubeQuotaOperation, int, CancellationToken>(
                (_, _, _, units, _) => QuotaUnits += units)
            .Returns(Task.CompletedTask);
    }

    public List<(string PlaylistId, string? VideoId, int? MaxResults)> Requests { get; } = [];

    /// <summary>When set, the videoId-filtered membership check fails with HTTP 500.</summary>
    public bool MembershipFails { get; set; }

    public Mock<IYouTubeQuotaUsageTracker> QuotaTracker { get; } = new();

    /// <summary>Quota units recorded by the services under test.</summary>
    public int QuotaUnits { get; private set; }

    /// <summary>Sets a playlist's items in playlist order; <paramref name="totalResults"/> defaults to the count.</summary>
    public FakeYouTubeHintsApi Playlist(string playlistId, IEnumerable<FakeYouTubeVideo> items, int? totalResults = null)
    {
        _playlists[playlistId] = items.ToList();
        if (totalResults.HasValue)
        {
            _totals[playlistId] = totalResults.Value;
        }
        else
        {
            _totals.Remove(playlistId);
        }

        return this;
    }

    public IYouTubeServiceWrapper Wrapper()
    {
        var wrapper = new AutoMocker().GetMock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.YouTubeService).Returns(CreateService());
        wrapper.SetupGet(x => x.CanRotate).Returns(true);
        return wrapper.Object;
    }

    private YouTubeService CreateService() =>
        new(new BaseClientService.Initializer
        {
            HttpClientFactory = new FakeHttpClientFactory(new Handler(this)),
            ApiKey = "synthetic-test-key"
        });

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var playlistId = query["playlistId"] ?? string.Empty;
        var videoId = query["videoId"];
        int? maxResults = int.TryParse(query["maxResults"], out var m) ? m : null;
        var offset = int.TryParse((query["pageToken"] ?? string.Empty).TrimStart('o'), out var o) ? o : 0;
        Requests.Add((playlistId, videoId, maxResults));
        if (Requests.Count > MaxRequests || !_playlists.TryGetValue(playlistId, out var list) ||
            (videoId != null && MembershipFails))
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{ \"error\": { \"code\": 500, \"message\": \"synthetic failure\" } }")
            };
        }

        var size = maxResults ?? 5;
        var page = videoId != null
            ? list.Where(x => x.Id == videoId).Take(1).ToList()
            : list.Skip(offset).Take(size).ToList();
        var body = new Dictionary<string, object?>
        {
            ["kind"] = "youtube#playlistItemListResponse",
            ["pageInfo"] = new
            {
                totalResults = videoId != null ? page.Count : _totals.GetValueOrDefault(playlistId, list.Count),
                resultsPerPage = size
            },
            ["items"] = page.Select(x =>
            {
                var published = x.PublishedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
                return new
                {
                    kind = "youtube#playlistItem",
                    id = $"item-{x.Id}",
                    snippet = new
                    {
                        title = $"Synthetic {x.Id}",
                        publishedAt = published,
                        playlistId,
                        resourceId = new { kind = "youtube#video", videoId = x.Id }
                    },
                    contentDetails = new { videoId = x.Id, videoPublishedAt = published }
                };
            }).ToList()
        };
        if (videoId == null && offset + size < list.Count)
        {
            body["nextPageToken"] = $"o{offset + size}";
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
    }

    private sealed class Handler(FakeYouTubeHintsApi api) : HttpMessageHandler
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

/// <summary>A clock fixed at one instant, for the full-walk interval rules.</summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
}
