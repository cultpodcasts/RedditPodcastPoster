using FluentAssertions;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Episodes.TestSupport;

namespace RedditPodcastPoster.EpisodeLookup.BusinessRules.Tests.BusinessRules;

/// <summary>
/// Strategy selection per flow x show size x release age (docs/catalogue-pagination.md, "Episode lookup planner").
/// Every plan is bounded by a date, a page cap or both.
/// </summary>
public class EpisodeLookupPlannerRules
{
    private static readonly DateTime Now = DomainTestFixture.UtcDateDaysAgo(0).AddHours(12);
    private const string Platform = CataloguePlatforms.Spotify;
    private readonly EpisodeLookupPlanner _sut = CatalogueTestServices.CreatePlanner(new FixedTimeProvider(Now));

    public static TheoryData<EpisodeLookupFlow, int?, int?, string> Matrix() => new()
    {
        // flow, show size, release days ago (null = undated), expected plan
        { EpisodeLookupFlow.Index, 50, 1, "full-show-read" },
        { EpisodeLookupFlow.Index, 200, 400, "full-show-read" },
        { EpisodeLookupFlow.Index, 201, 1, "newest-pages" },
        { EpisodeLookupFlow.Index, null, 1, "newest-pages" },
        { EpisodeLookupFlow.Index, null, 400, "newest-pages" },
        { EpisodeLookupFlow.Submit, 120, 400, "full-show-read>title-search" },
        { EpisodeLookupFlow.Submit, null, 1, "newest-pages>title-search>bounded-walk" },
        { EpisodeLookupFlow.Submit, 600, 13, "newest-pages>title-search>bounded-walk" },
        { EpisodeLookupFlow.Submit, 600, 15, "title-search>bounded-walk" },
        { EpisodeLookupFlow.Submit, null, 400, "title-search>bounded-walk" },
        { EpisodeLookupFlow.Submit, null, null, "title-search>bounded-walk" },
        { EpisodeLookupFlow.Discovery, 199, 30, "full-show-read>title-search" },
        { EpisodeLookupFlow.Discovery, 600, 1, "newest-pages>title-search>bounded-walk" },
        { EpisodeLookupFlow.Discovery, 600, 90, "title-search>bounded-walk" },
        { EpisodeLookupFlow.Enrich, 0, 90, "full-show-read>title-search" },
        { EpisodeLookupFlow.Enrich, null, 90, "title-search>bounded-walk" },
        { EpisodeLookupFlow.Enrich, 600, 2, "newest-pages>title-search>bounded-walk" },
    };

    [Theory(DisplayName =
        "Plan picks: full read for known shows of <=200 episodes in any flow (outside Index, time-budgeted with a title-search fallback); Index newest pages (no search) for larger/unknown shows; " +
        "other flows newest page then search then bounded walk for releases inside 14 days, else search then bounded walk.")]
    [MemberData(nameof(Matrix))]
    public void Plan_matrix(EpisodeLookupFlow flow, int? showSize, int? releasedDaysAgo, string expected)
    {
        // Arrange
        var lookup = Lookup(flow, releasedDaysAgo is { } d ? Now.AddDays(-d) : null, Now.AddDays(-7));

        // Act
        var plan = _sut.Plan(lookup, hasShow: true, showSize, Platform);

        // Assert
        string.Join(">", plan.Select(x => x.Stage)).Should().Be(expected);
        plan.Where(x => x.Strategy != CatalogueLookupStrategy.TitleSearch)
            .Should().OnlyContain(x => x.PageCap != null, "every page read is bounded (pagination HARD rule)");
        plan.Where(x => x.Strategy == CatalogueLookupStrategy.FullShowRead)
            .Should().OnlyContain(x => x.PageCap == new EpisodeLookupOptions().FullShowReadMaxPages);
    }

    [Theory(DisplayName =
        "Outside Index, a small-show full read carries the FullShowReadBudget so it cannot hold a request for minutes " +
        "(submission 4a7d8838: The Compendium, Spotify 185 and Apple 159 episodes, ~45 s per platform per item, twice); " +
        "Index keeps its unbudgeted full read with no fallback.")]
    [InlineData(EpisodeLookupFlow.Submit, true)]
    [InlineData(EpisodeLookupFlow.Discovery, true)]
    [InlineData(EpisodeLookupFlow.Enrich, true)]
    [InlineData(EpisodeLookupFlow.Index, false)]
    public void Full_read_budget_per_flow(EpisodeLookupFlow flow, bool budgeted)
    {
        // Arrange
        var options = new EpisodeLookupOptions { FullShowReadBudget = TimeSpan.FromSeconds(7) };
        var sut = CatalogueTestServices.CreatePlanner(new FixedTimeProvider(Now), options);
        var lookup = Lookup(flow, Now.AddDays(-400), Now.AddDays(-7));

        // Act
        var plan = sut.Plan(lookup, hasShow: true, 185, CataloguePlatforms.Apple);

        // Assert
        var fullRead = plan.Should().ContainSingle(x => x.Strategy == CatalogueLookupStrategy.FullShowRead).Subject;
        fullRead.Budget.Should().Be(budgeted ? TimeSpan.FromSeconds(7) : null);
        plan.Select(x => x.Strategy).Should().Equal(budgeted
            ? [CatalogueLookupStrategy.FullShowRead, CatalogueLookupStrategy.TitleSearch]
            : [CatalogueLookupStrategy.FullShowRead]);
    }

    [Theory(DisplayName =
        "When no show id is known for the platform, the Index flow plans nothing, because a title search cannot succeed " +
        "(hits must match the show name exactly) and indexing never learns a show id from it " +
        "(2026-10-13: 19 hourly Spotify searches for YouTube-only podcasts such as Narcissists Playbook).")]
    [InlineData(CataloguePlatforms.Spotify)]
    [InlineData(CataloguePlatforms.Apple)]
    public void Index_without_show_plans_nothing(string platform)
    {
        // Arrange
        var lookup = new CatalogueLookup(
            "Narcissists Playbook",
            "Narcissist's Playbook: People Need to Know They're Not Crazy",
            Now.AddDays(-2),
            TimeSpan.FromMinutes(12))
        {
            Flow = EpisodeLookupFlow.Index,
            WindowStart = Now.AddDays(-3)
        };

        // Act
        var plan = _sut.Plan(lookup, hasShow: false, showEpisodeCount: null, platform);

        // Assert
        plan.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "When no show id is known, Submit, Discovery and Enrich still plan a title search only, because there is no feed " +
        "to read and a submitted, discovered or operator-chosen episode may be the way its show is first found.")]
    [InlineData(EpisodeLookupFlow.Submit, CataloguePlatforms.Spotify)]
    [InlineData(EpisodeLookupFlow.Discovery, CataloguePlatforms.Spotify)]
    [InlineData(EpisodeLookupFlow.Enrich, CataloguePlatforms.Spotify)]
    [InlineData(EpisodeLookupFlow.Submit, CataloguePlatforms.Apple)]
    [InlineData(EpisodeLookupFlow.Discovery, CataloguePlatforms.Apple)]
    [InlineData(EpisodeLookupFlow.Enrich, CataloguePlatforms.Apple)]
    public void No_show_is_search_only_outside_index(EpisodeLookupFlow flow, string platform)
    {
        // Arrange
        var lookup = Lookup(flow, Now.AddDays(-1), null);

        // Act
        var plan = _sut.Plan(lookup, hasShow: false, 50, platform);

        // Assert
        plan.Should().ContainSingle().Which.Strategy.Should().Be(CatalogueLookupStrategy.TitleSearch);
    }

    [Fact(DisplayName =
        "Index newest pages stop at the earlier of the window start and the release band start (release - 14 days), capped at IndexMaxPages, " +
        "so a YouTube release that predates its audio by up to 14 days is still reached.")]
    public void Index_stop_at_is_earliest_of_window_and_band()
    {
        // Arrange
        var released = Now.AddDays(-3);
        var window = Now.AddDays(-7);

        // Act
        var step = _sut.Plan(Lookup(EpisodeLookupFlow.Index, released, window), true, null, Platform).Single();

        // Assert
        step.StopAt.Should().Be(EpisodeReleaseTolerance.GetSubmitMatchBand(released).Start);
        step.PageCap.Should().Be(new EpisodeLookupOptions().IndexMaxPages);
    }

    [Fact(DisplayName = "An undated Index lookup with no window reads only the newest page, because nothing else bounds the read.")]
    public void Index_without_dates_reads_one_page()
    {
        // Act
        var step = _sut.Plan(Lookup(EpisodeLookupFlow.Index, null, null), true, null, Platform).Single();

        // Assert
        step.StopAt.Should().BeNull();
        step.PageCap.Should().Be(1);
    }

    [Fact(DisplayName =
        "The bounded walk uses the platform's configured page cap (Spotify 4, Apple 5 by default) and stops at the release band start, so an old episode costs at most that many pages.")]
    public void Bounded_walk_uses_platform_cap_and_band_start()
    {
        // Arrange
        var released = Now.AddDays(-90);

        // Act
        var spotify = _sut.Plan(Lookup(EpisodeLookupFlow.Submit, released, null), true, null, CataloguePlatforms.Spotify).Last();
        var apple = _sut.Plan(Lookup(EpisodeLookupFlow.Submit, released, null), true, null, CataloguePlatforms.Apple).Last();

        // Assert
        spotify.Strategy.Should().Be(CatalogueLookupStrategy.BoundedWalk);
        spotify.PageCap.Should().Be(4);
        apple.PageCap.Should().Be(5);
        spotify.StopAt.Should().Be(EpisodeReleaseTolerance.GetSubmitMatchBand(released).Start);
    }

    [Fact(DisplayName = "A configured FullShowReadMaxEpisodes of 0 disables the full read, because only an empty show qualifies.")]
    public void Full_read_threshold_is_configurable()
    {
        // Arrange
        var sut = CatalogueTestServices.CreatePlanner(new FixedTimeProvider(Now), new EpisodeLookupOptions { FullShowReadMaxEpisodes = 0 });

        // Act
        var plan = sut.Plan(Lookup(EpisodeLookupFlow.Submit, Now.AddDays(-90), null), true, 50, Platform);

        // Assert
        plan.Select(x => x.Strategy).Should().NotContain(CatalogueLookupStrategy.FullShowRead);
    }

    private static CatalogueLookup Lookup(EpisodeLookupFlow flow, DateTime? released, DateTime? window) =>
        new("Podcast", "Lantern Harbour Night Stories", released, TimeSpan.FromMinutes(44))
        {
            Flow = flow,
            WindowStart = window
        };

    [Fact(DisplayName =
        "Every use case declares a large-show chain for both release ages in EpisodeLookupChains, so a new flow cannot silently fall back.")]
    public void Every_flow_declares_its_chains()
    {
        // Arrange
        var flows = Enum.GetValues<EpisodeLookupFlow>();

        // Act
        var missing = flows.SelectMany(f => Enum.GetValues<EpisodeLookupAge>().Select(a => (f, a)))
            .Where(key => !EpisodeLookupChains.LargeShow.ContainsKey(key))
            .ToList();

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact(DisplayName = "Configured bounded-walk caps and Index page cap are used instead of the defaults, so tuning needs no code change.")]
    public void Caps_come_from_options()
    {
        // Arrange
        var sut = CatalogueTestServices.CreatePlanner(new FixedTimeProvider(Now),
            new EpisodeLookupOptions { SpotifyBoundedWalkPages = 2, AppleBoundedWalkPages = 7, IndexMaxPages = 3 });

        // Act
        var spotify = sut.Plan(Lookup(EpisodeLookupFlow.Submit, Now.AddDays(-90), null), true, null, CataloguePlatforms.Spotify).Last();
        var apple = sut.Plan(Lookup(EpisodeLookupFlow.Submit, Now.AddDays(-90), null), true, null, CataloguePlatforms.Apple).Last();
        var index = sut.Plan(Lookup(EpisodeLookupFlow.Index, Now.AddDays(-1), Now.AddDays(-7)), true, null, CataloguePlatforms.Apple).Single();

        // Assert
        spotify.PageCap.Should().Be(2);
        apple.PageCap.Should().Be(7);
        index.PageCap.Should().Be(3);
    }
}
