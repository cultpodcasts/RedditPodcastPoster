using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Tests.Support;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Paginators;

public class AscendingEpisodePaginatorRules
{
    private const string CataloguePath = "https://api.spotify.com/v1/shows/show/episodes";
    private const int PageLimit = 50;

    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An ascending Spotify catalogue jumps directly to its final page and walks backwards to ReleasedSince " +
        "because recent episodes are at the end and paging forward from offset zero wastes quota.")]
    public async Task Jumps_to_final_page_then_walks_back_to_cutoff()
    {
        // Arrange — 103 episodes means the final 50-item page begins at offset 100
        var firstHref = PageUrl(0);
        var finalUrl = PageUrl(100);
        var previousUrl = PageUrl(50);
        var recentOne = CreateEpisode(daysAgo: 1);
        var recentTwo = CreateEpisode(daysAgo: 2);
        var beyondCutoff = CreateEpisode(daysAgo: 30);
        var firstPage = new Paging<SimpleEpisode>
        {
            Href = firstHref,
            Items = [CreateEpisode(daysAgo: 3000)],
            Limit = PageLimit,
            Offset = 0,
            Total = 103,
            Next = previousUrl
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>
        {
            [finalUrl] = new Paging<SimpleEpisode>
            {
                Href = finalUrl,
                Items = [recentTwo, recentOne],
                Limit = PageLimit,
                Offset = 100,
                Total = 103,
                Previous = previousUrl
            },
            [previousUrl] = new Paging<SimpleEpisode>
            {
                Href = previousUrl,
                Items = [beyondCutoff],
                Limit = PageLimit,
                Offset = 50,
                Total = 103,
                Previous = firstHref
            }
        });
        var sut = CreateSut(DomainTestFixture.UtcDateDaysAgo(3));

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Select(x => x.Id).Should().BeEquivalentTo(recentOne.Id, recentTwo.Id);
        connector.RequestedUrls.Should().Equal(finalUrl, previousUrl);
        connector.RequestedUrls.Should().NotContain(firstHref);
    }

    [Fact(DisplayName =
        "An exact multiple catalogue size jumps to total minus limit " +
        "because offset equal to total would be an empty page.")]
    public async Task Exact_multiple_uses_last_non_empty_offset()
    {
        // Arrange — 100 episodes at 50 per page means the newest page begins at offset 50
        var firstHref = PageUrl(0);
        var finalUrl = PageUrl(PageLimit);
        var recent = CreateEpisode(daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Href = firstHref,
            Items = [CreateEpisode(daysAgo: 3000)],
            Limit = PageLimit,
            Offset = 0,
            Total = 100
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>
        {
            [finalUrl] = new Paging<SimpleEpisode>
            {
                Href = finalUrl,
                Items = [recent, CreateEpisode(daysAgo: 30)],
                Limit = PageLimit,
                Offset = PageLimit,
                Total = 100,
                Previous = firstHref
            }
        });
        var sut = CreateSut(DomainTestFixture.UtcDateDaysAgo(3));

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Select(x => x.Id).Should().Equal(recent.Id);
        connector.RequestedUrls.Should().Equal(finalUrl);
    }

    [Fact(DisplayName =
        "A backwards walk through pages that never leave the release window stops after MaxWalkBackPages fetches " +
        "because reading back from the newest end needs far fewer pages than a forward crawl and must not burn Spotify quota.")]
    public async Task Walk_back_stops_at_its_own_page_cap()
    {
        // Arrange — every page sits inside the window, so only the cap can end the walk
        var (firstPage, pagesByUrl) = BuildInWindowAscendingCatalogue();
        var connector = new FakeSpotifyApiConnector(pagesByUrl);
        var sut = CreateSut(DomainTestFixture.UtcDateDaysAgo(3));

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert — the end jump plus MaxWalkBackPages backwards fetches, and no further page
        connector.RequestedUrls.Should().HaveCount(AscendingEpisodePaginator.MaxWalkBackPages + 1);
        results.Should().HaveCount(AscendingEpisodePaginator.MaxWalkBackPages + 1);
    }

    [Fact(DisplayName =
        "When the backwards walk stops on its cap with earlier pages remaining the paginator logs an Error " +
        "because a quota circuit-breaker trip can hide in-window episodes and must not be silent.")]
    public async Task Logs_error_when_walk_back_cap_trips()
    {
        // Arrange
        var (firstPage, pagesByUrl) = BuildInWindowAscendingCatalogue();
        var logger = new CapturingLogger();
        var sut = CreateSut(DomainTestFixture.UtcDateDaysAgo(3), logger);

        // Act
        await sut.Paginate(firstPage, new FakeSpotifyApiConnector(pagesByUrl)).ToListAsync();

        // Assert — reported against the walk-back cap, not the forward-crawl cap
        logger.Errors.Should().ContainSingle(m =>
            m.StartsWith(SimpleEpisodePaginator.CircuitBreakerTrippedMessagePrefix) &&
            m.Contains($"pages-fetched='{AscendingEpisodePaginator.MaxWalkBackPages}'") &&
            m.Contains($"max-pages='{AscendingEpisodePaginator.MaxWalkBackPages}'") &&
            m.Contains("walk-back='true'"));
    }

    [Fact(DisplayName =
        "A backwards walk that reaches the release-window cutoff logs no circuit-breaker Error " +
        "because leaving the window is the intended stop, not quota protection.")]
    public async Task Does_not_log_error_when_walk_back_reaches_cutoff()
    {
        // Arrange — the page before the newest one falls outside the window
        var firstHref = PageUrl(0);
        var finalUrl = PageUrl(PageLimit);
        var recent = CreateEpisode(daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Href = firstHref,
            Items = [CreateEpisode(daysAgo: 3000)],
            Limit = PageLimit,
            Offset = 0,
            Total = PageLimit * 2
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>
        {
            [finalUrl] = new Paging<SimpleEpisode>
            {
                Href = finalUrl,
                Items = [CreateEpisode(daysAgo: 30), recent],
                Limit = PageLimit,
                Offset = PageLimit,
                Total = PageLimit * 2,
                Previous = firstHref
            }
        });
        var logger = new CapturingLogger();
        var sut = CreateSut(DomainTestFixture.UtcDateDaysAgo(3), logger);

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Select(x => x.Id).Should().Equal(recent.Id);
        logger.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When Spotify omits paging totals the ascending paginator hands over to the bounded forward walk " +
        "because without Total and Limit there is no way to address the final page.")]
    public async Task Falls_back_to_forward_walk_when_paging_metadata_is_missing()
    {
        // Arrange — no Total, so the end jump cannot be computed
        var nextUrl = PageUrl(PageLimit);
        var firstEpisode = CreateEpisode(daysAgo: 2);
        var nextEpisode = CreateEpisode(daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Href = PageUrl(0),
            Items = [firstEpisode],
            Limit = PageLimit,
            Offset = 0,
            Next = nextUrl
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>
        {
            [nextUrl] = new Paging<SimpleEpisode> { Items = [nextEpisode], Next = null }
        });
        var sut = CreateSut(DomainTestFixture.UtcDateDaysAgo(3));

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert — forward walk yields the first page too, and follows Next rather than Previous
        results.Select(x => x.Id).Should().Equal(firstEpisode.Id, nextEpisode.Id);
        connector.RequestedUrls.Should().Equal(nextUrl);
    }

    /// <summary>
    /// Builds an ascending catalogue whose every page is inside the release window and links back to an
    /// earlier page, so a backwards walk can only be stopped by the walk-back cap.
    /// </summary>
    private (Paging<SimpleEpisode> FirstPage, Dictionary<string, object> PagesByUrl)
        BuildInWindowAscendingCatalogue()
    {
        var pageCount = AscendingEpisodePaginator.MaxWalkBackPages + 3;
        var total = pageCount * PageLimit;
        var pagesByUrl = new Dictionary<string, object>();
        Paging<SimpleEpisode>? firstPage = null;

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var offset = pageIndex * PageLimit;
            var page = new Paging<SimpleEpisode>
            {
                Href = PageUrl(offset),
                Items = [CreateEpisode(daysAgo: 1)],
                Limit = PageLimit,
                Offset = offset,
                Total = total,
                Previous = pageIndex == 0 ? null : PageUrl(offset - PageLimit),
                Next = pageIndex == pageCount - 1 ? null : PageUrl(offset + PageLimit)
            };

            if (pageIndex == 0)
            {
                firstPage = page;
            }
            else
            {
                pagesByUrl[PageUrl(offset)] = page;
            }
        }

        return (firstPage!, pagesByUrl);
    }

    private static string PageUrl(int offset) => $"{CataloguePath}?limit={PageLimit}&offset={offset}";

    private static AscendingEpisodePaginator CreateSut(
        DateTime releasedSince,
        ILogger<AscendingEpisodePaginator>? logger = null) =>
        new(
            releasedSince,
            logger ?? NullLogger<AscendingEpisodePaginator>.Instance,
            new SimpleEpisodePaginator(
                releasedSince,
                isInReverseOrder: false,
                NullLogger<SimpleEpisodePaginator>.Instance));

    private SimpleEpisode CreateEpisode(int daysAgo) =>
        new()
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(daysAgo).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode
        };

    private sealed class CapturingLogger : ILogger<AscendingEpisodePaginator>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
