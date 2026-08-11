using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using AppleEpisodeRecord = RedditPodcastPoster.PodcastServices.Apple.Models.Record;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.BusinessRules.Providers;

/// <summary>
/// ApplePodcastService must early-stop newest-first catalogues on ReleasedSince even when
/// consecutive episodes share a release timestamp (common same-day batching), and must hard-cap
/// unordered date-scoped walks so SubmitUrl MatchOtherServices cannot page thousands of episodes.
/// </summary>
public class ApplePodcastServicePaginationRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a newest-first Apple catalogue has equal release timestamps on page one and ReleasedSince " +
        "is already past the oldest item on that page, GetEpisodes does not follow next " +
        "because equal dates must not force a full-catalogue MatchOtherServices walk.")]
    public async Task equal_dates_newest_first_stops_at_released_since_without_following_next()
    {
        // Arrange
        var podcastId = _fixture.CreateAppleId();
        var sameRelease = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var releasedSince = sameRelease.AddDays(1);
        var page1 = CreatePage(
            [
                CreateRecord(_fixture.CreateAppleId(), sameRelease),
                CreateRecord(_fixture.CreateAppleId(), sameRelease)
            ],
            next: $"/v1/catalog/us/podcasts/{podcastId}/episodes?offset=2");
        var handler = new ScriptedAppleHandler(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [$"/v1/catalog/us/podcasts/{podcastId}/episodes"] = page1
            });
        var sut = CreateSut(handler);

        // Act
        var episodes = await sut.GetEpisodes(
            new ApplePodcastId(podcastId),
            new IndexingContext { ReleasedSince = releasedSince });

        // Assert
        episodes.Should().NotBeNull();
        episodes!.Should().HaveCount(2);
        handler.RequestPaths.Should().ContainSingle()
            .Which.Should().Be($"/v1/catalog/us/podcasts/{podcastId}/episodes");
    }

    [Fact(DisplayName =
        "When a newest-first Apple catalogue still has in-window episodes at the end of page one, " +
        "GetEpisodes follows next until the oldest collected release falls before ReleasedSince.")]
    public async Task newest_first_follows_next_while_last_collected_is_in_window()
    {
        // Arrange
        var podcastId = _fixture.CreateAppleId();
        var newer = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());
        var mid = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var older = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var releasedSince = DomainTestFixture.UtcDateDaysAgo(3);
        var nextPath = $"/v1/catalog/us/podcasts/{podcastId}/episodes?offset=2";
        var page1 = CreatePage(
            [
                CreateRecord(_fixture.CreateAppleId(), newer),
                CreateRecord(_fixture.CreateAppleId(), mid)
            ],
            next: nextPath);
        var page2 = CreatePage(
            [CreateRecord(_fixture.CreateAppleId(), older)],
            next: null);
        var handler = new ScriptedAppleHandler(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [$"/v1/catalog/us/podcasts/{podcastId}/episodes"] = page1,
                [nextPath] = page2
            });
        var sut = CreateSut(handler);

        // Act
        var episodes = await sut.GetEpisodes(
            new ApplePodcastId(podcastId),
            new IndexingContext { ReleasedSince = releasedSince });

        // Assert
        episodes.Should().NotBeNull();
        episodes!.Should().HaveCount(3);
        handler.RequestPaths.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "When MatchOtherServices resolves Apple for a recent YouTube- or Spotify-authority episode on a newest-first " +
        "show whose catalogue has thousands of older pages, GetEpisodes fetches only the in-window head page " +
        "because ReleasedSince early-stop must not walk the full Apple catalogue on SubmitUrl -m.")]
    public async Task submiturl_match_other_services_recent_episode_pages_only_in_window_head()
    {
        // Arrange — page 1 ends below ReleasedSince; dozens of older next links remain available
        var podcastId = _fixture.CreateAppleId();
        var recentRelease = DomainTestFixture.UtcAtTime(0, _fixture.CreateNonMidnightTimeOfDay());
        var publishingDelay = TimeSpan.FromHours(6);
        // UrlCategoriser YouTube→Apple: ReleasedSince = YT release − delay (same shape for recent Spotify − 1 day)
        var releasedSince = recentRelease.Subtract(publishingDelay);
        var justOutsideWindow = releasedSince.AddMinutes(-30);
        var pagesByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const int olderCataloguePages = 40; // >> MaxPages; stands in for a multi-thousand-episode show
        string? firstNext = null;
        for (var page = 1; page <= olderCataloguePages; page++)
        {
            var path = page == 1
                ? $"/v1/catalog/us/podcasts/{podcastId}/episodes"
                : $"/v1/catalog/us/podcasts/{podcastId}/episodes?offset={page}";
            var nextPath = page < olderCataloguePages
                ? $"/v1/catalog/us/podcasts/{podcastId}/episodes?offset={page + 1}"
                : null;
            firstNext ??= nextPath;
            if (page == 1)
            {
                // Newest-first head: recent match candidate, then an older item that trips ReleasedSince stop
                pagesByPath[path] = CreatePage(
                    [
                        CreateRecord(_fixture.CreateAppleId(), recentRelease),
                        CreateRecord(_fixture.CreateAppleId(), justOutsideWindow)
                    ],
                    next: nextPath);
            }
            else
            {
                pagesByPath[path] = CreatePage(
                    [CreateRecord(_fixture.CreateAppleId(), recentRelease.AddDays(-page * 7))],
                    next: nextPath);
            }
        }

        var handler = new ScriptedAppleHandler(pagesByPath);
        var sut = CreateSut(handler);

        // Act
        var episodes = await sut.GetEpisodes(
            new ApplePodcastId(podcastId),
            new IndexingContext { ReleasedSince = releasedSince });

        // Assert — only the head page; older catalogue links are never followed
        episodes.Should().NotBeNull();
        episodes!.Should().HaveCount(2);
        handler.RequestPaths.Should().ContainSingle()
            .Which.Should().Be($"/v1/catalog/us/podcasts/{podcastId}/episodes");
        firstNext.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the Apple catalogue head is ascending and ReleasedSince is set, GetEpisodes stops after MaxPages " +
        "subsequent fetches even though next remains " +
        "because unordered date-scoped walks must not page an entire high-volume show.")]
    public async Task ascending_with_released_since_stops_after_max_pages_subsequent_fetches()
    {
        // Arrange
        var podcastId = _fixture.CreateAppleId();
        var older = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var newer = older.AddHours(4);
        var releasedSince = DomainTestFixture.UtcDateDaysAgo(30);
        var pagesByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var totalSubsequent = AppleCataloguePagination.MaxPages + 3;
        for (var page = 0; page <= totalSubsequent; page++)
        {
            var path = page == 0
                ? $"/v1/catalog/us/podcasts/{podcastId}/episodes"
                : $"/v1/catalog/us/podcasts/{podcastId}/episodes?offset={page}";
            var nextPath = page < totalSubsequent
                ? $"/v1/catalog/us/podcasts/{podcastId}/episodes?offset={page + 1}"
                : null;
            // Page 1 ascending probe (older then newer); later pages keep offering next
            var records = page == 0
                ? new[]
                {
                    CreateRecord(_fixture.CreateAppleId(), older),
                    CreateRecord(_fixture.CreateAppleId(), newer)
                }
                : new[] { CreateRecord(_fixture.CreateAppleId(), newer.AddDays(page)) };
            pagesByPath[path] = CreatePage(records, next: nextPath);
        }

        var handler = new ScriptedAppleHandler(pagesByPath);
        var sut = CreateSut(handler);

        // Act
        var episodes = await sut.GetEpisodes(
            new ApplePodcastId(podcastId),
            new IndexingContext { ReleasedSince = releasedSince });

        // Assert — first page + MaxPages subsequent = MaxPages + 1 requests
        episodes.Should().NotBeNull();
        handler.RequestPaths.Should().HaveCount(AppleCataloguePagination.MaxPages + 1);
    }

    [Fact(DisplayName =
        "When ReleasedSince is null, GetEpisodes follows next past MaxPages " +
        "because full-catalogue Apple callers still need the entire feed.")]
    public async Task without_released_since_follows_next_past_max_pages()
    {
        // Arrange
        var podcastId = _fixture.CreateAppleId();
        var pagesByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var totalPages = AppleCataloguePagination.MaxPages + 4;
        for (var page = 0; page < totalPages; page++)
        {
            var path = page == 0
                ? $"/v1/catalog/us/podcasts/{podcastId}/episodes"
                : $"/v1/catalog/us/podcasts/{podcastId}/episodes?offset={page}";
            var nextPath = page < totalPages - 1
                ? $"/v1/catalog/us/podcasts/{podcastId}/episodes?offset={page + 1}"
                : null;
            var release = DomainTestFixture.UtcAtTime(-page, _fixture.CreateNonMidnightTimeOfDay());
            pagesByPath[path] = CreatePage(
                [CreateRecord(_fixture.CreateAppleId(), release)],
                next: nextPath);
        }

        var handler = new ScriptedAppleHandler(pagesByPath);
        var sut = CreateSut(handler);

        // Act
        var episodes = await sut.GetEpisodes(
            new ApplePodcastId(podcastId),
            new IndexingContext());

        // Assert
        episodes.Should().NotBeNull();
        handler.RequestPaths.Should().HaveCount(totalPages);
    }

    private static ApplePodcastService CreateSut(ScriptedAppleHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://amp-api.podcasts.apple.com")
        };
        return new ApplePodcastService(
            new StubHttpClientProvider(client),
            NullLogger<ApplePodcastService>.Instance);
    }

    private AppleEpisodeRecord CreateRecord(long episodeId, DateTime release) =>
        new()
        {
            Id = episodeId.ToString(),
            Attributes = new Attributes
            {
                Name = _fixture.CreateTitle(),
                Released = release,
                LengthMs = (long)_fixture.CreateDuration().TotalMilliseconds,
                Url = $"https://podcasts.apple.com/us/podcast/episode/id{_fixture.CreateAppleId()}?i={episodeId}",
                Description = new Description { Standard = _fixture.Create<string>() }
            }
        };

    private static string CreatePage(IEnumerable<AppleEpisodeRecord> records, string? next) =>
        JsonSerializer.Serialize(new PodcastResponse
        {
            Next = next ?? string.Empty,
            Records = records.ToList()
        });

    private sealed class StubHttpClientProvider(HttpClient client) : IAsyncInstance<HttpClient>
    {
        public Task<HttpClient> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(client);
    }

    private sealed class ScriptedAppleHandler(IReadOnlyDictionary<string, string> responsesByPath)
        : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.IsAbsoluteUri
                ? request.RequestUri.PathAndQuery
                : request.RequestUri.ToString();
            RequestPaths.Add(path);
            if (!responsesByPath.TryGetValue(path, out var body))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent($"missing script for {path}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
