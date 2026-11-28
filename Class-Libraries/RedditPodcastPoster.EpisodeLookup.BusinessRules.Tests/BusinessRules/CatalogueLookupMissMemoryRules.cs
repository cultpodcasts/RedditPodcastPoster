using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EpisodeLookup.BusinessRules.Tests.BusinessRules;

/// <summary>
/// Index-only miss memory: skip a platform lookup while the catalogue fingerprint is unchanged within the retry window.
/// </summary>
public class CatalogueLookupMissMemoryRules
{
    private readonly DomainTestFixture _fixture = new();
    private static readonly DateTime Now = DomainTestFixture.UtcDateDaysAgo(0).AddHours(12);
    /// <summary>Probe title that must not fuzzy-match the unrelated catalogue rows below.</summary>
    private const string ProbeTitle = "Lantern Harbour Night Stories With A Guest From The Archive";

    public sealed record Item(string Id, string Title, DateTime Release);

    private sealed class CountingClassifier : ICatalogueSubjectClassifier
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<string>?> ClassifyAsync(
            Episode probe,
            IReadOnlyList<Episode> candidates,
            CatalogueLookup lookup,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(lookup.IgnoredSubjects);
        }
    }

    private sealed class FakeSource(
        IReadOnlyList<Item> newestFirst,
        int? reportedTotal = null,
        int failFromPage = int.MaxValue,
        string? showId = "show",
        string platform = CataloguePlatforms.Spotify,
        IReadOnlyList<Item>? searchHits = null) : ICatalogueCandidateSource<Item>
    {
        public int PageCalls { get; private set; }
        public int SearchCalls { get; private set; }

        public string Platform => platform;
        public string? ShowId => showId;

        public Task<int?> ProbeShowEpisodeCount(CancellationToken cancellationToken) =>
            Task.FromResult(reportedTotal);

        public Task<CatalogueShowPage<Item>?> GetNewestPage(int pageIndex, CancellationToken cancellationToken)
        {
            PageCalls++;
            if (pageIndex >= failFromPage)
            {
                return Task.FromResult<CatalogueShowPage<Item>?>(null);
            }

            var items = newestFirst.Skip(pageIndex * 50).Take(50).ToList();
            return Task.FromResult<CatalogueShowPage<Item>?>(new CatalogueShowPage<Item>(
                items, (pageIndex + 1) * 50 < newestFirst.Count, reportedTotal));
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

    private List<Item> UnrelatedShow(int count, string? newestId = null)
    {
        var newest = newestId ?? _fixture.CreateSpotifyId();
        return Enumerable.Range(0, count)
            .Select(i => new Item(
                i == 0 ? newest : _fixture.CreateSpotifyId(),
                $"Unrelated Episode Number {i} About Volcanic Origami",
                Now.Date.AddDays(-i)))
            .ToList();
    }

    private CatalogueLookup IndexLookup(
        Guid podcastId,
        Guid episodeId,
        int? showCount,
        IReadOnlyList<EpisodeLookupMiss>? knownMisses,
        EpisodeLookupFlow flow = EpisodeLookupFlow.Index,
        string? title = null) =>
        new("Podcast", title ?? ProbeTitle, Now.Date.AddDays(-2), TimeSpan.FromMinutes(44), Service.Spotify)
        {
            Flow = flow,
            WindowStart = Now.AddDays(-7),
            ShowEpisodeCount = showCount,
            PodcastId = podcastId,
            EpisodeId = episodeId,
            KnownMisses = knownMisses
        };

    private static CatalogueLookupExecutor Sut(
        TimeProvider? clock = null,
        ICatalogueLookupMisses? misses = null,
        ICatalogueSubjectClassifier? classifier = null,
        EpisodeLookupOptions? options = null,
        ILogger<CatalogueLookupExecutor>? logger = null) =>
        CatalogueTestServices.CreateExecutor(
            subjectClassifier: classifier,
            timeProvider: clock ?? new FixedTimeProvider(Now),
            options: options,
            lookupMisses: misses,
            logger: logger);

    [Fact(DisplayName =
        "An Index lookup that missed is skipped on the next pass while the newest catalogue episode and the show total are unchanged.")]
    public async Task Unchanged_fingerprint_skips()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(600);
        var newestId = show[0].Id;
        const int total = 600;
        var source = new FakeSource(show, reportedTotal: total);
        var classifier = new CountingClassifier();
        var known = new EpisodeLookupMiss
        {
            EpisodeId = episodeId,
            Platform = CataloguePlatforms.Spotify,
            NewestCatalogueId = newestId,
            ShowTotal = total,
            LastFullAttemptUtc = Now.AddHours(-1)
        };

        // Act
        var result = await Sut(classifier: classifier).FindMatch(
            IndexLookup(podcastId, episodeId, total, [known]), source);

        // Assert
        result.Stage.Should().Be(CatalogueLookupExecutor.StageSkippedUnchanged);
        result.Match.Should().BeNull();
        source.PageCalls.Should().Be(1);
        source.SearchCalls.Should().Be(0);
        classifier.Calls.Should().Be(0);
    }

    [Fact(DisplayName = "A new newest catalogue episode runs the full lookup again.")]
    public async Task New_newest_runs_full_lookup()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(600);
        const int total = 600;
        var source = new FakeSource(show, reportedTotal: total);
        var classifier = new CountingClassifier();
        var known = new EpisodeLookupMiss
        {
            EpisodeId = episodeId,
            Platform = CataloguePlatforms.Spotify,
            NewestCatalogueId = _fixture.CreateSpotifyId(),
            ShowTotal = total,
            LastFullAttemptUtc = Now.AddHours(-1)
        };

        // Act
        var result = await Sut(classifier: classifier).FindMatch(
            IndexLookup(podcastId, episodeId, total, [known]), source);

        // Assert
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
        classifier.Calls.Should().BeGreaterThan(0);
        source.PageCalls.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "A changed show total runs the full lookup again.")]
    public async Task Changed_show_total_runs_full_lookup()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(600);
        var source = new FakeSource(show, reportedTotal: 601);
        var known = new EpisodeLookupMiss
        {
            EpisodeId = episodeId,
            Platform = CataloguePlatforms.Spotify,
            NewestCatalogueId = show[0].Id,
            ShowTotal = 600,
            LastFullAttemptUtc = Now.AddHours(-1)
        };

        // Act
        var result = await Sut().FindMatch(
            IndexLookup(podcastId, episodeId, 601, [known]), source);

        // Assert
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
    }

    [Fact(DisplayName =
        "A miss older than 24 hours runs the full lookup again even when the catalogue is unchanged.")]
    public async Task Stale_miss_runs_full_lookup()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(600);
        const int total = 600;
        var source = new FakeSource(show, reportedTotal: total);
        var classifier = new CountingClassifier();
        var known = new EpisodeLookupMiss
        {
            EpisodeId = episodeId,
            Platform = CataloguePlatforms.Spotify,
            NewestCatalogueId = show[0].Id,
            ShowTotal = total,
            LastFullAttemptUtc = Now.AddHours(-25)
        };

        // Act
        var result = await Sut(classifier: classifier).FindMatch(
            IndexLookup(podcastId, episodeId, total, [known]), source);

        // Assert
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
        classifier.Calls.Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "A lookup that matches clears the remembered miss for that episode and platform.")]
    public async Task Match_clears_remembered_miss()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(10);
        show = show.Prepend(new Item(_fixture.CreateSpotifyId(), ProbeTitle, Now.Date.AddDays(-1))).ToList();
        const int total = 11;
        var source = new FakeSource(show, reportedTotal: total);
        var misses = new CatalogueLookupMisses();
        var known = new EpisodeLookupMiss
        {
            EpisodeId = episodeId,
            Platform = CataloguePlatforms.Spotify,
            NewestCatalogueId = _fixture.CreateSpotifyId(),
            ShowTotal = total,
            LastFullAttemptUtc = Now.AddHours(-1)
        };

        // Act
        var result = await Sut(misses: misses).FindMatch(
            IndexLookup(podcastId, episodeId, total, [known]), source);

        // Assert
        result.Match.Should().NotBeNull();
        misses.TryGetChanges(podcastId, out var changes).Should().BeTrue();
        changes.Should().ContainSingle(c =>
            c.EpisodeId == episodeId &&
            c.Platform == CataloguePlatforms.Spotify &&
            c.Clear);
    }

    [Fact(DisplayName = "A Spotify miss does not skip the Apple lookup for the same episode.")]
    public async Task Platforms_are_independent()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(600);
        const int total = 600;
        var appleSource = new FakeSource(show, reportedTotal: total, platform: CataloguePlatforms.Apple);
        var classifier = new CountingClassifier();
        var known = new EpisodeLookupMiss
        {
            EpisodeId = episodeId,
            Platform = CataloguePlatforms.Spotify,
            NewestCatalogueId = show[0].Id,
            ShowTotal = total,
            LastFullAttemptUtc = Now.AddHours(-1)
        };

        // Act
        var result = await Sut(classifier: classifier).FindMatch(
            IndexLookup(podcastId, episodeId, total, [known]), appleSource);

        // Assert
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
        classifier.Calls.Should().BeGreaterThan(0);
    }

    [Theory(DisplayName =
        "Submit, discovery and enrich flows never skip a lookup because of a remembered miss.")]
    [InlineData(EpisodeLookupFlow.Submit)]
    [InlineData(EpisodeLookupFlow.Discovery)]
    [InlineData(EpisodeLookupFlow.Enrich)]
    public async Task Non_index_flows_never_skip(EpisodeLookupFlow flow)
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(600);
        const int total = 600;
        var source = new FakeSource(show, reportedTotal: total, searchHits: []);
        var classifier = new CountingClassifier();
        var known = new EpisodeLookupMiss
        {
            EpisodeId = episodeId,
            Platform = CataloguePlatforms.Spotify,
            NewestCatalogueId = show[0].Id,
            ShowTotal = total,
            LastFullAttemptUtc = Now.AddHours(-1)
        };

        // Act
        var result = await Sut(classifier: classifier).FindMatch(
            IndexLookup(podcastId, episodeId, total, [known], flow), source);

        // Assert
        result.Stage.Should().NotBe(CatalogueLookupExecutor.StageSkippedUnchanged);
        classifier.Calls.Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "When the newest page cannot be read the lookup is not skipped.")]
    public async Task Unreadable_page0_does_not_skip()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(600);
        const int total = 600;
        var source = new FakeSource(show, reportedTotal: total, failFromPage: 0);
        var classifier = new CountingClassifier();
        var known = new EpisodeLookupMiss
        {
            EpisodeId = episodeId,
            Platform = CataloguePlatforms.Spotify,
            NewestCatalogueId = show[0].Id,
            ShowTotal = total,
            LastFullAttemptUtc = Now.AddHours(-1)
        };

        // Act
        var result = await Sut(classifier: classifier).FindMatch(
            IndexLookup(podcastId, episodeId, total, [known]), source);

        // Assert
        result.Stage.Should().NotBe(CatalogueLookupExecutor.StageSkippedUnchanged);
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
    }

    [Fact(DisplayName =
        "A first Index miss records the catalogue fingerprint so the next pass can skip while it is unchanged.")]
    public async Task Miss_records_fingerprint()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var show = UnrelatedShow(600);
        const int total = 600;
        var source = new FakeSource(show, reportedTotal: total);
        var misses = new CatalogueLookupMisses();

        // Act
        var result = await Sut(misses: misses).FindMatch(
            IndexLookup(podcastId, episodeId, total, knownMisses: null), source);

        // Assert
        result.Stage.Should().Be(CatalogueLookupExecutor.StageMiss);
        misses.TryGetChanges(podcastId, out var changes).Should().BeTrue();
        changes.Should().ContainSingle(c =>
            !c.Clear &&
            c.EpisodeId == episodeId &&
            c.Platform == CataloguePlatforms.Spotify &&
            c.NewestCatalogueId == show[0].Id &&
            c.ShowTotal == total);
    }
}
