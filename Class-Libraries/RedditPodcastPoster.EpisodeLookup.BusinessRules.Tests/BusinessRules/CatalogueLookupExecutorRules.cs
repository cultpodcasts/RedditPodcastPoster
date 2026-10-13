using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EpisodeLookup.BusinessRules.Tests.BusinessRules;

/// <summary>
/// The shared executor runs a plan over any platform source: pages are read once and reused, ids are tried once,
/// reads stop at the date or the cap, a failed (null) page ends the read, and observed show sizes are recorded.
/// </summary>
public class CatalogueLookupExecutorRules
{
    private static readonly DateTime Now = DomainTestFixture.UtcDateDaysAgo(0).AddHours(12);
    private const string Title = "Lantern Harbour Night Stories With A Guest From The Archive";

    public sealed record Item(string Id, string Title, DateTime Release);

    /// <summary>Daily show, newest first, pages of 50, with call counters.</summary>
    private sealed class FakeSource(
        IReadOnlyList<Item> newestFirst,
        IReadOnlyList<Item>? searchHits = null,
        int? probeCount = null,
        int? reportedTotal = null,
        bool newestFirstPages = true,
        int failFromPage = int.MaxValue,
        string? showId = "show",
        string platform = CataloguePlatforms.Spotify) : ICatalogueCandidateSource<Item>
    {
        public int PageCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public int ProbeCalls { get; private set; }

        public string Platform => platform;
        public string? ShowId => showId;

        public Task<int?> ProbeShowEpisodeCount(CancellationToken cancellationToken)
        {
            ProbeCalls++;
            return Task.FromResult(probeCount);
        }

        public Task<CatalogueShowPage<Item>?> GetNewestPage(int pageIndex, CancellationToken cancellationToken)
        {
            PageCalls++;
            if (pageIndex >= failFromPage)
            {
                return Task.FromResult<CatalogueShowPage<Item>?>(null);
            }

            var items = newestFirst.Skip(pageIndex * 50).Take(50).ToList();
            return Task.FromResult<CatalogueShowPage<Item>?>(new CatalogueShowPage<Item>(
                items, (pageIndex + 1) * 50 < newestFirst.Count, reportedTotal, newestFirstPages));
        }

        public Task<IReadOnlyList<Item>> SearchByTitle(CancellationToken cancellationToken)
        {
            SearchCalls++;
            return Task.FromResult(searchHits ?? []);
        }

        public string GetId(Item item) => item.Id;

        public Episode ToCatalogueEpisode(Item item) => new()
        {
            Title = item.Title,
            Length = TimeSpan.FromMinutes(44),
            ReleaseUtc = item.Release
        };
    }

    private static List<Item> Show(int count, int? targetIndex = null) =>
        Enumerable.Range(0, count)
            .Select(i => i == targetIndex
                ? new Item($"id{i}", Title, Now.Date.AddDays(-i))
                : new Item($"id{i}", $"Unrelated Episode Number {i} About Volcanic Origami", Now.Date.AddDays(-i)))
            .ToList();

    private static CatalogueLookup Lookup(EpisodeLookupFlow flow, int daysAgo, int? storedCount = null) =>
        new("Podcast", Title, Now.Date.AddDays(-daysAgo), TimeSpan.FromMinutes(44), Service.Spotify)
        {
            Flow = flow,
            WindowStart = Now.AddDays(-7),
            ShowEpisodeCount = storedCount
        };

    private static CatalogueLookupExecutor Sut(ICatalogueShowEpisodeCounts? counts = null, ILogger<CatalogueLookupExecutor>? logger = null) =>
        CatalogueTestServices.CreateExecutor(timeProvider: new FixedTimeProvider(Now), showEpisodeCounts: counts, logger: logger);

    [Fact(DisplayName =
        "When a recent release misses the newest page and the title search, the bounded walk reuses page 0 instead of fetching it again.")]
    public async Task Pages_are_reused_across_steps()
    {
        // Arrange
        var source = new FakeSource(Show(600, targetIndex: 60));

        // Act
        var result = await Sut().FindMatch(Lookup(EpisodeLookupFlow.Submit, 2, storedCount: 600), source);

        // Assert
        result.Match.Should().BeNull("the target is outside the band of a 2-day-old release");
        source.PageCalls.Should().Be(1, "page 0 already passes the band start, so the walk stops there too");
        source.SearchCalls.Should().Be(1);
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
    }

    [Fact(DisplayName = "The Index flow reads newest pages until one is older than the stop date, then stops without searching.")]
    public async Task Index_stops_at_date()
    {
        // Arrange
        var source = new FakeSource(Show(600, targetIndex: 3));

        // Act
        var result = await Sut().FindMatch(Lookup(EpisodeLookupFlow.Index, 3, storedCount: 600), source);

        // Assert
        result.Match!.Id.Should().Be("id3");
        result.Stage.Should().Be("newest-pages");
        source.PageCalls.Should().Be(1);
        source.SearchCalls.Should().Be(0);
    }

    [Fact(DisplayName = "A search hit returns the search stage and FromSearch, and no pages are read for an old episode of a known large show.")]
    public async Task Search_hit_reads_no_pages()
    {
        // Arrange
        var target = new Item("hit", Title, Now.Date.AddDays(-300));
        var source = new FakeSource(Show(600), searchHits: [target]);

        // Act
        var result = await Sut().FindMatch(Lookup(EpisodeLookupFlow.Discovery, 300, storedCount: 600), source);

        // Assert
        result.Match.Should().BeSameAs(target);
        result.Stage.Should().Be("title-search");
        result.FromSearch.Should().BeTrue();
        source.PageCalls.Should().Be(0);
        source.ProbeCalls.Should().Be(0, "a stored count needs no probe");
    }

    [Fact(DisplayName = "The bounded walk stops at the platform's configured page cap (Apple 5) when the release date is not reached.")]
    public async Task Bounded_walk_stops_at_cap()
    {
        // Arrange
        var source = new FakeSource(Show(600), platform: CataloguePlatforms.Apple);

        // Act
        var result = await Sut().FindMatch(Lookup(EpisodeLookupFlow.Enrich, 500, storedCount: 600), source);

        // Assert
        result.Match.Should().BeNull();
        source.PageCalls.Should().Be(5);
    }

    [Fact(DisplayName =
        "Oldest-first pages turn the date early-stop off, so the walk is bounded by the page cap alone and still finds an in-cap episode.")]
    public async Task Oldest_first_pages_ignore_date_stop()
    {
        // Arrange
        var source = new FakeSource(Show(600, targetIndex: 120), newestFirstPages: false);

        // Act
        var result = await Sut().FindMatch(Lookup(EpisodeLookupFlow.Submit, 120, storedCount: 600), source);

        // Assert
        result.Match!.Id.Should().Be("id120");
        result.Stage.Should().Be("bounded-walk");
    }

    [Fact(DisplayName =
        "When the podcast has no stored count, the executor probes once, plans with the probed size and records it for the podcast document.")]
    public async Task Unknown_size_is_probed_and_recorded()
    {
        // Arrange
        var counts = new CatalogueShowEpisodeCounts();
        var source = new FakeSource(Show(120, targetIndex: 110), probeCount: 120);

        // Act
        var result = await Sut(counts).FindMatch(Lookup(EpisodeLookupFlow.Submit, 110), source);

        // Assert
        source.ProbeCalls.Should().Be(1);
        result.Stage.Should().Be("full-show-read");
        result.Match!.Id.Should().Be("id110");
        counts.TryGet(CataloguePlatforms.Spotify, "show", out var recorded).Should().BeTrue();
        recorded.Should().Be(120);
    }

    [Fact(DisplayName = "A completed full read records the exact number of items read, overriding a stale stored count.")]
    public async Task Full_read_records_exact_count()
    {
        // Arrange
        var counts = new CatalogueShowEpisodeCounts();
        var source = new FakeSource(Show(130));

        // Act
        await Sut(counts).FindMatch(Lookup(EpisodeLookupFlow.Submit, 400, storedCount: 150), source);

        // Assert
        counts.TryGet(CataloguePlatforms.Spotify, "show", out var recorded).Should().BeTrue();
        recorded.Should().Be(130);
    }

    [Fact(DisplayName = "A platform-reported total on any page read is recorded (Spotify Paging.Total), even on a partial read.")]
    public async Task Reported_total_is_recorded()
    {
        // Arrange
        var counts = new CatalogueShowEpisodeCounts();
        var source = new FakeSource(Show(600), reportedTotal: 600);

        // Act
        await Sut(counts).FindMatch(Lookup(EpisodeLookupFlow.Index, 1, storedCount: 590), source);

        // Assert
        counts.TryGet(CataloguePlatforms.Spotify, "show", out var recorded).Should().BeTrue();
        recorded.Should().Be(600);
    }

    [Fact(DisplayName =
        "When a page fails or times out (null), the read ends there and the lookup falls through to the next step and then a clean miss.")]
    public async Task Failed_page_is_clean_miss()
    {
        // Arrange
        var source = new FakeSource(Show(600), failFromPage: 0);

        // Act
        var result = await Sut().FindMatch(Lookup(EpisodeLookupFlow.Submit, 1, storedCount: 600), source);

        // Assert
        result.Match.Should().BeNull();
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
        source.PageCalls.Should().Be(1, "an exhausted read is not retried by the bounded walk");
        source.SearchCalls.Should().Be(1);
    }

    [Fact(DisplayName = "A candidate already tried in an earlier step is not matched again in a later step.")]
    public async Task Tried_ids_are_not_rematched()
    {
        // Arrange
        var target = new Item("id1", Title, Now.Date.AddDays(-1));
        var source = new FakeSource(Show(600), searchHits: [target]);
        var reduced = 0;

        // Act
        var result = await Sut().FindMatch(Lookup(EpisodeLookupFlow.Submit, 1, storedCount: 600), source,
            reducer: x => { if (x.Id == "id1") reduced++; return false; });

        // Assert
        result.Match.Should().BeNull();
        reduced.Should().BeLessThanOrEqualTo(1);
    }

    [Fact(DisplayName = "Each lookup logs one line with platform, use case (flow), plan, matching strategy (stage), show size and pages read.")]
    public async Task Logs_one_summary_line()
    {
        // Arrange
        var logger = new RecordingLogger<CatalogueLookupExecutor>();
        var source = new FakeSource(Show(600, targetIndex: 3));

        // Act
        await Sut(logger: logger).FindMatch(Lookup(EpisodeLookupFlow.Index, 3, storedCount: 600), source);

        // Assert
        logger.Entries.Should().ContainSingle(x =>
            x.Level == LogLevel.Information &&
            x.Message.StartsWith(CatalogueLookupExecutor.LogMessagePrefix) &&
            x.Message.Contains("platform='Spotify'") &&
            x.Message.Contains("flow='Index'") &&
            x.Message.Contains("plan='newest-pages'") &&
            x.Message.Contains("stage='newest-pages'") &&
            x.Message.Contains("show-episodes=600") &&
            x.Message.Contains("pages-read=1"));
    }

    [Theory(DisplayName =
        "An Index lookup with no show id makes no catalogue calls and logs plan 'none', stage 'skipped-no-show' and " +
        "pages-read=0, so YouTube-only podcasts cost nothing each hour and the skip is still visible in App Insights.")]
    [InlineData(CataloguePlatforms.Spotify)]
    [InlineData(CataloguePlatforms.Apple)]
    public async Task Index_without_show_is_skipped(string platform)
    {
        // Arrange
        var logger = new RecordingLogger<CatalogueLookupExecutor>();
        var source = new FakeSource(Show(10, targetIndex: 2), searchHits: Show(10, targetIndex: 2), probeCount: 10,
            showId: null, platform: platform);

        // Act
        var result = await Sut(logger: logger).FindMatch(Lookup(EpisodeLookupFlow.Index, 2), source);

        // Assert
        result.Match.Should().BeNull();
        result.Stage.Should().Be(CatalogueLookupExecutor.StageSkippedNoShow);
        source.SearchCalls.Should().Be(0);
        source.PageCalls.Should().Be(0);
        source.ProbeCalls.Should().Be(0);
        logger.Entries.Should().ContainSingle(x =>
            x.Level == LogLevel.Information &&
            x.Message.StartsWith(CatalogueLookupExecutor.LogMessagePrefix) &&
            x.Message.Contains($"platform='{platform}'") &&
            x.Message.Contains("flow='Index'") &&
            x.Message.Contains("plan='none'") &&
            x.Message.Contains("stage='skipped-no-show'") &&
            x.Message.Contains("pages-read=0"));
    }

    [Fact(DisplayName = "A Submit lookup with no show id still searches by title and can match, because submission can discover a show.")]
    public async Task Submit_without_show_still_searches()
    {
        // Arrange
        var source = new FakeSource([], searchHits: Show(5, targetIndex: 1), showId: null);

        // Act
        var result = await Sut().FindMatch(Lookup(EpisodeLookupFlow.Submit, 1), source);

        // Assert
        source.SearchCalls.Should().Be(1);
        result.Match.Should().NotBeNull();
        result.FromSearch.Should().BeTrue();
    }
}
