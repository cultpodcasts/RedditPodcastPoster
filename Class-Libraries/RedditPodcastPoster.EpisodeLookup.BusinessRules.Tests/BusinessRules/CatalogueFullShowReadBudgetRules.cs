using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EpisodeLookup.BusinessRules.Tests.BusinessRules;

/// <summary>
/// Small-show full reads outside Index are time-budgeted (EpisodeLookupOptions.FullShowReadBudget), from the real case:
/// submission 4a7d8838 (apply 13 Oct 22:57 BST). The Compendium (Spotify 185 episodes / 4 pages, Apple 159 / 16 pages
/// of 10) cost ~45 s per platform per item, mostly per-candidate subject classification for YouTube-discovered
/// episodes, and two items from that show pushed the synchronous apply past the client's patience.
/// </summary>
public class CatalogueFullShowReadBudgetRules
{
    private static readonly DateTime Now = DomainTestFixture.UtcDateDaysAgo(0).AddHours(12);
    private const string Title = "Heaven's Gate Cult: Between Devotion and Delusion";
    private const int ApplePageSize = 10;

    public sealed record Item(string Id, string Title, DateTime Release);

    /// <summary>Clock the fakes advance, so the budget is deterministic.</summary>
    private sealed class ManualClock(DateTime start) : TimeProvider
    {
        private DateTimeOffset _now = new(DateTime.SpecifyKind(start, DateTimeKind.Utc), TimeSpan.Zero);
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>Apple-like show: newest first, pages of 10; each page costs <paramref name="perPage"/>.</summary>
    private sealed class PagedSource(
        IReadOnlyList<Item> newestFirst,
        ManualClock clock,
        TimeSpan perPage,
        IReadOnlyList<Item>? searchHits = null) : ICatalogueCandidateSource<Item>
    {
        public int PageCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public string Platform => CataloguePlatforms.Apple;
        public string? ShowId => "1520000000";
        public Task<int?> ProbeShowEpisodeCount(CancellationToken cancellationToken) => Task.FromResult<int?>(null);

        public Task<CatalogueShowPage<Item>?> GetNewestPage(int pageIndex, CancellationToken cancellationToken)
        {
            PageCalls++;
            clock.Advance(perPage);
            var items = newestFirst.Skip(pageIndex * ApplePageSize).Take(ApplePageSize).ToList();
            return Task.FromResult<CatalogueShowPage<Item>?>(new CatalogueShowPage<Item>(
                items, (pageIndex + 1) * ApplePageSize < newestFirst.Count));
        }

        public Task<IReadOnlyList<Item>> SearchByTitle(CancellationToken cancellationToken)
        {
            SearchCalls++;
            return Task.FromResult(searchHits ?? []);
        }

        public string GetId(Item item) => item.Id;

        public Episode ToCatalogueEpisode(Item item) => new()
        {
            Title = item.Title, Length = TimeSpan.FromMinutes(52), ReleaseUtc = item.Release
        };
    }

    /// <summary>Classifier whose cost is per candidate (the real one runs every subject's terms over each).</summary>
    private sealed class PerCandidateCostClassifier(ManualClock clock, TimeSpan perCandidate) : ICatalogueSubjectClassifier
    {
        public int Classified { get; private set; }

        public Task<IReadOnlyList<string>?> ClassifyAsync(Episode probe, IReadOnlyList<Episode> candidates,
            CatalogueLookup lookup, CancellationToken cancellationToken)
        {
            Classified += candidates.Count;
            clock.Advance(perCandidate * candidates.Count);
            return Task.FromResult(lookup.IgnoredSubjects);
        }
    }

    private static List<Item> Show(int count, int? targetIndex = null) =>
        Enumerable.Range(0, count)
            .Select(i => new Item($"id{i}", i == targetIndex ? Title : $"Unrelated Story {i} About Tidal Clocks",
                Now.Date.AddDays(-7 * i)))
            .ToList();

    private static CatalogueLookup Lookup(EpisodeLookupFlow flow, DateTime released) =>
        new("The Compendium: An Assembly of Fascinating Things", Title, released, TimeSpan.FromMinutes(52), Service.YouTube,
            EnrichingYouTubeDiscoveredEpisode: true)
        {
            Flow = flow,
            ShowEpisodeCount = 159
        };

    private static CatalogueLookupExecutor Sut(ManualClock clock, ICatalogueSubjectClassifier classifier,
        ILogger<CatalogueLookupExecutor>? logger = null, TimeSpan? budget = null) =>
        CatalogueTestServices.CreateExecutor(
            subjectClassifier: classifier,
            timeProvider: clock,
            options: new EpisodeLookupOptions { FullShowReadBudget = budget ?? TimeSpan.FromSeconds(20) },
            logger: logger);

    [Theory(DisplayName =
        "INVARIANT (submission 4a7d8838): Outside Index, a full read of a 159-episode Apple show that would take ~45 s " +
        "stops when the 20 s budget is spent and falls back to a title search, which can still match.")]
    [InlineData(EpisodeLookupFlow.Submit)]
    [InlineData(EpisodeLookupFlow.Discovery)]
    [InlineData(EpisodeLookupFlow.Enrich)]
    public async Task Budget_hit_falls_back_to_title_search(EpisodeLookupFlow flow)
    {
        // Arrange
        var clock = new ManualClock(Now);
        var show = Show(159, targetIndex: 150);
        var source = new PagedSource(show, clock, TimeSpan.FromMilliseconds(500), searchHits: [show[150]]);
        var classifier = new PerCandidateCostClassifier(clock, TimeSpan.FromMilliseconds(250));
        var logger = new RecordingLogger<CatalogueLookupExecutor>();

        // Act
        var result = await Sut(clock, classifier, logger).FindMatch(Lookup(flow, show[150].Release), source);

        // Assert
        result.Match.Should().Be(show[150]);
        result.Stage.Should().Be("title-search");
        source.PageCalls.Should().BeLessThan(16, "the read stopped before the end of the show");
        source.SearchCalls.Should().Be(1);
        (clock.GetUtcNow().UtcDateTime - Now).Should().BeLessThan(TimeSpan.FromSeconds(25));
        logger.Entries.Should().Contain(x =>
            x.Level == LogLevel.Information && x.Message.StartsWith(CatalogueLookupExecutor.BudgetLogMessagePrefix) &&
            x.Message.Contains("platform='Apple'") && x.Message.Contains($"flow='{flow}'") &&
            x.Message.Contains("stage='full-show-read'") && x.Message.Contains("budget-seconds=20"));
    }

    [Fact(DisplayName =
        "When the budget is spent and the title search also misses, the lookup is a clean miss (stage 'miss'), never an error.")]
    public async Task Budget_hit_then_search_miss_is_clean_miss()
    {
        // Arrange
        var clock = new ManualClock(Now);
        var source = new PagedSource(Show(159, targetIndex: 150), clock, TimeSpan.FromMilliseconds(500));
        var classifier = new PerCandidateCostClassifier(clock, TimeSpan.FromMilliseconds(250));

        // Act
        var result = await Sut(clock, classifier).FindMatch(Lookup(EpisodeLookupFlow.Submit, Now.AddDays(-400)), source);

        // Assert
        result.Match.Should().BeNull();
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
    }

    [Fact(DisplayName =
        "A full read that completes within the budget matches as before, and a completed miss skips the title search, " +
        "because every episode of the show has already been tried.")]
    public async Task Completed_full_read_skips_search()
    {
        // Arrange
        var clock = new ManualClock(Now);
        var show = Show(38, targetIndex: 30);
        var hit = new PagedSource(show, clock, TimeSpan.FromMilliseconds(100));
        var miss = new PagedSource(Show(38), clock, TimeSpan.FromMilliseconds(100));
        var classifier = new PerCandidateCostClassifier(clock, TimeSpan.FromMilliseconds(10));
        var sut = Sut(clock, classifier);

        // Act
        var matched = await sut.FindMatch(Lookup(EpisodeLookupFlow.Submit, show[30].Release), hit);
        var missed = await sut.FindMatch(Lookup(EpisodeLookupFlow.Submit, show[30].Release), miss);

        // Assert
        matched.Match.Should().Be(show[30]);
        matched.Stage.Should().Be("full-show-read");
        missed.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
        miss.PageCalls.Should().Be(4);
        miss.SearchCalls.Should().Be(0);
    }

    [Fact(DisplayName =
        "INVARIANT: The Index flow's full read is not budgeted (it runs in the background) and never title-searches.")]
    public async Task Index_full_read_is_unbudgeted()
    {
        // Arrange
        var clock = new ManualClock(Now);
        var show = Show(159, targetIndex: 150);
        var source = new PagedSource(show, clock, TimeSpan.FromSeconds(5), searchHits: [show[150]]);
        var classifier = new PerCandidateCostClassifier(clock, TimeSpan.FromMilliseconds(250));

        // Act
        var result = await Sut(clock, classifier).FindMatch(Lookup(EpisodeLookupFlow.Index, show[150].Release), source);

        // Assert
        result.Match.Should().Be(show[150]);
        result.Stage.Should().Be("full-show-read");
        source.PageCalls.Should().Be(16);
        source.SearchCalls.Should().Be(0);
    }
}
