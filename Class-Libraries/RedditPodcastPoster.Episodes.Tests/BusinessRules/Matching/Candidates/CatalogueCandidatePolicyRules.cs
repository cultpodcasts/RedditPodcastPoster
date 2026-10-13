using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching.Candidates;

/// <summary>
/// Stage order of the shared catalogue candidate policy (platform-agnostic; the real Spotify and Apple adapters are
/// exercised by the catalogue contract tests).
/// </summary>
public class CatalogueCandidatePolicyRules
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private const string Title = "Lantern Harbour Night Stories With A Guest From The Archive";

    [Fact(DisplayName =
        "When the release is recent and the show is known, FindMatch matches from one page of the show feed " +
        "with the configured page size and does not search.")]
    public async Task Recent_release_matches_from_show_feed_without_search()
    {
        // Arrange
        var target = Audio(Title, Now.AddDays(-1));
        var source = new FakeSource(hasShow: true, recent: [Audio("Volcanic Origami Workshop", Now), target], search: []);
        var sut = CatalogueTestServices.CreatePolicy(timeProvider: new FixedTimeProvider(Now));

        // Act
        var result = await sut.FindMatch(Lookup(Now.AddDays(-1)), source);

        // Assert
        result.Should().BeSameAs(target);
        source.RecentCalls.Should().Be(1);
        source.SearchCalls.Should().Be(0);
        source.RequestedPageSize.Should().Be(new CatalogueCandidatePolicyOptions().RecentShowEpisodesPageSize);
    }

    [Fact(DisplayName =
        "When the release is outside the recent window, FindMatch skips the show feed and uses the search fallback.")]
    public async Task Older_release_uses_search_fallback()
    {
        // Arrange
        var target = Audio(Title, Now.AddDays(-120));
        var source = new FakeSource(hasShow: true, recent: [target], search: [target]);
        var sut = CatalogueTestServices.CreatePolicy(timeProvider: new FixedTimeProvider(Now));

        // Act
        var result = await sut.FindMatch(Lookup(Now.AddDays(-120)), source);

        // Assert
        result.Should().BeSameAs(target);
        source.RecentCalls.Should().Be(0);
        source.SearchCalls.Should().Be(1);
    }

    [Fact(DisplayName =
        "When the show is unknown, FindMatch does not read a show feed and goes straight to search.")]
    public async Task No_show_goes_straight_to_search()
    {
        // Arrange
        var source = new FakeSource(hasShow: false, recent: [Audio(Title, Now.AddDays(-1))], search: []);
        var sut = CatalogueTestServices.CreatePolicy(timeProvider: new FixedTimeProvider(Now));

        // Act
        var result = await sut.FindMatch(Lookup(Now.AddDays(-1)), source);

        // Assert
        result.Should().BeNull();
        source.RecentCalls.Should().Be(0);
        source.SearchCalls.Should().Be(1);
    }

    [Fact(DisplayName =
        "When the recent page has no match, FindMatch falls back to search and does not re-match candidates already tried.")]
    public async Task Recent_page_miss_falls_back_to_search_without_retrying()
    {
        // Arrange
        var unrelated = Audio("Volcanic Origami Workshop", Now.AddDays(-1));
        var target = Audio(Title, Now.AddDays(-3));
        var source = new FakeSource(hasShow: true, recent: [unrelated], search: [unrelated, target]);
        var matcher = new RecordingMatcher(new CatalogueEpisodeMatcher(EpisodeDomainTestServices.CreatePlatformMatcher()));
        var sut = new CatalogueCandidatePolicy(
            matcher, new NoCatalogueSubjectClassifier(), Options.Create(new CatalogueCandidatePolicyOptions()),
            new FixedTimeProvider(Now), Microsoft.Extensions.Logging.Abstractions.NullLogger<CatalogueCandidatePolicy>.Instance);

        // Act
        var result = await sut.FindMatch(Lookup(Now.AddDays(-3)), source);

        // Assert
        result.Should().BeSameAs(target);
        matcher.Batches.Should().HaveCount(2);
        matcher.Batches[1].Should().ContainSingle().Which.Title.Should().Be(Title);
    }

    [Fact(DisplayName =
        "The recent window and page size come from CatalogueCandidatePolicyOptions, so a shorter configured window sends a " +
        "three-day-old release to search.")]
    public async Task Options_bind_recent_window()
    {
        // Arrange
        var target = Audio(Title, Now.AddDays(-3));
        var source = new FakeSource(hasShow: true, recent: [target], search: [target]);
        var sut = CatalogueTestServices.CreatePolicy(
            timeProvider: new FixedTimeProvider(Now),
            options: new CatalogueCandidatePolicyOptions { RecentWindow = TimeSpan.FromDays(2), RecentShowEpisodesPageSize = 20 });

        // Act
        var result = await sut.FindMatch(Lookup(Now.AddDays(-3)), source);

        // Assert
        result.Should().BeSameAs(target);
        source.RecentCalls.Should().Be(0);
        source.SearchCalls.Should().Be(1);
    }

    [Theory(DisplayName =
        "FindMatch logs which stage matched (recent-show-feed, search-fallback or miss) with platform, podcast and candidate counts.")]
    [InlineData(true, CatalogueCandidatePolicy.StageRecentShowFeed)]
    [InlineData(false, CatalogueCandidatePolicy.StageSearchFallback)]
    public async Task Logs_matching_stage(bool inFeed, string expectedStage)
    {
        // Arrange
        var target = Audio(Title, Now.AddDays(-1));
        var source = new FakeSource(hasShow: true, recent: inFeed ? [target] : [], search: [target]);
        var logger = new CapturingLogger();
        var sut = new CatalogueCandidatePolicy(
            new CatalogueEpisodeMatcher(EpisodeDomainTestServices.CreatePlatformMatcher()),
            new NoCatalogueSubjectClassifier(), Options.Create(new CatalogueCandidatePolicyOptions()),
            new FixedTimeProvider(Now), logger);

        // Act
        await sut.FindMatch(Lookup(Now.AddDays(-1)), source);

        // Assert
        logger.Messages.Should().ContainSingle()
            .Which.Should().Contain($"stage='{expectedStage}'").And.Contain("platform='Fake'");
    }

    private static CatalogueLookup Lookup(DateTime released) =>
        new("Podcast", Title, released, TimeSpan.FromMinutes(44), Service.Spotify);

    private static Episode Audio(string title, DateTime release) =>
        new() { Id = Guid.NewGuid(), Title = title, Length = TimeSpan.FromMinutes(44), ReleaseUtc = release, Description = string.Empty };

    private sealed class FakeSource(bool hasShow, IReadOnlyList<Episode> recent, IReadOnlyList<Episode> search)
        : ICatalogueCandidateSource<Episode>
    {
        public int RecentCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public int? RequestedPageSize { get; private set; }
        public string Platform => "Fake";
        public bool HasShow => hasShow;

        public Task<IReadOnlyList<Episode>> GetRecentShowEpisodes(int pageSize, CancellationToken cancellationToken)
        {
            RecentCalls++;
            RequestedPageSize = pageSize;
            return Task.FromResult<IReadOnlyList<Episode>>(recent.Take(pageSize).ToList());
        }

        public Task<IReadOnlyList<Episode>> SearchEpisodes(CancellationToken cancellationToken)
        {
            SearchCalls++;
            return Task.FromResult(search);
        }

        public string GetId(Episode item) => item.Id.ToString();

        public Episode ToCatalogueEpisode(Episode item) => item;
    }

    private sealed class RecordingMatcher(ICatalogueEpisodeMatcher inner) : ICatalogueEpisodeMatcher
    {
        public List<IReadOnlyList<Episode>> Batches { get; } = [];

        public Episode? Match(CatalogueLookup lookup, Episode probe, IReadOnlyList<Episode> candidates,
            IReadOnlyList<string>? ignoredSubjects, Func<Episode, bool>? reducer = null)
        {
            Batches.Add(candidates);
            return inner.Match(lookup, probe, candidates, ignoredSubjects, reducer);
        }
    }

    private sealed class CapturingLogger : ILogger<CatalogueCandidatePolicy>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
