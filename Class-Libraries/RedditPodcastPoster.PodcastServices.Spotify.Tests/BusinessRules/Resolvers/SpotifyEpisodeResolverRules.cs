using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Resolvers;

/// <summary>
/// SpotifyEpisodeResolver is a thin adapter: SkipSpotifyUrlResolving short-circuits, a known episode id is fetched
/// directly, otherwise the real shared <see cref="CatalogueLookupExecutor"/> runs the planned strategy chain
/// (full read for small shows, newest pages, title search, bounded walk of <see cref="EpisodeLookupOptions.SpotifyBoundedWalkPages"/>
/// pages) over pass-cached show pages; the match is hydrated with GetFullEpisode and dropped when not free.
/// Platform-identical outcomes are covered by the catalogue contract tests.
/// </summary>
public class SpotifyEpisodeResolverRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public SpotifyEpisodeResolverRules()
    {
        var guard = CatalogueTestServices.CreateGuard();
        _mocker.Use<ICatalogueRequestGuard>(guard);
        _mocker.Use<ICatalogueLookupExecutor>(CatalogueTestServices.CreateExecutor());
        _mocker.Use<ISpotifyShowEpisodePages>(
            new SpotifyShowEpisodePages(_mocker.GetMock<ISpotifyClientWrapper>().Object, guard));
        _mocker.Use<IHtmlSanitiser>(new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance));
    }

    [Fact(DisplayName =
        "When SkipSpotifyUrlResolving is set, FindEpisode returns no episode without calling Spotify clients " +
        "because rate-limit recovery must not start another episode lookup.")]
    public async Task Skip_spotify_url_resolving_returns_empty_without_client_calls()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();
        var request = Request(episodeSpotifyId: _fixture.CreateSpotifyId());

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext { SkipSpotifyUrlResolving = true });

        // Assert
        result.FullEpisode.Should().BeNull();
        _mocker.GetMock<ISpotifyClientWrapper>().VerifyNoOtherCalls();
        _mocker.GetMock<ISpotifyEpisodeTitleSearch>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "When EpisodeSpotifyId is set and GetFullEpisode succeeds, FindEpisode returns that episode without a catalogue lookup " +
        "because a direct episode-id lookup is cheaper than matching.")]
    public async Task Direct_episode_id_returns_full_episode_without_catalogue_lookup()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var full = new FullEpisode { Id = episodeId, Name = _fixture.CreateTitle(), IsPlayable = true };
        StubFullEpisode(full);
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(episodeSpotifyId: episodeId), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(full);
        VerifyNoCatalogueLookup();
    }

    [Fact(DisplayName =
        "When EpisodeSpotifyId is set but GetFullEpisode returns null, FindEpisode falls through to the shared catalogue lookup " +
        "because a stale or invalid stored id must not block matching.")]
    public async Task Episode_id_miss_falls_through_to_catalogue_lookup()
    {
        // Arrange
        var title = "Lantern Harbour Night Stories With A Guest From The Archive";
        var released = DomainTestFixture.UtcDateDaysAgo(2);
        var match = Simple(_fixture.CreateSpotifyId(), title, released, TimeSpan.FromMinutes(44));
        StubShowPage([match]);
        var full = StubFullEpisode(new FullEpisode { Id = match.Id, Name = title, IsPlayable = true });
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(
            Request(title, released, episodeSpotifyId: _fixture.CreateSpotifyId()), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(full);
    }

    [Fact(DisplayName =
        "When a recent YouTube-authority episode's title differs from the audio-feed title (Steve Hassan / Ben McKenzie, 12 Oct 2026), " +
        "FindEpisode matches it from the recent show page read first and makes no title search " +
        "because the shared policy reads the show feed before searching.")]
    public async Task Divergent_youtube_title_matches_from_recent_show_page_without_search()
    {
        // Arrange
        var released = DateTime.UtcNow.Date.AddDays(-1).AddHours(8);
        var audio = Simple("3RtQ7heOajiCNdikDXUTlW",
            "Everyone Is Lying to You for Money, Crypto and Cult Psychology with Ben McKenzie: My conversation with the author of Easy Money about financial manipulation, the need to belong, and protecting our freedom of mind.",
            released, new TimeSpan(0, 59, 29));
        var previousWeek = Simple(_fixture.CreateSpotifyId(), "Volcanic Origami Workshop", released.AddDays(-7), new TimeSpan(0, 58, 10));
        StubShowPage([audio, previousWeek]);
        var full = StubFullEpisode(new FullEpisode { Id = audio.Id, Name = audio.Name, IsPlayable = true });
        _mocker.Use<ICatalogueLookupExecutor>(CatalogueTestServices.CreateExecutor(BenMcKenzieSubjects()));
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();
        var request = Request(
            "Why Is Everyone Lying to You for Money? The Great Crypto Scam EXPOSED (w/ Ben McKenzie)",
            released,
            podcastSpotifyId: "3fy5dEPagR2T8MPeS7ieAM",
            releaseAuthority: Service.YouTube,
            length: new TimeSpan(0, 56, 49),
            enrichingYouTubeDiscoveredEpisode: true);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(full);
        VerifyShowPage(Times.Once());
        VerifyTitleSearch(Times.Never());
    }

    [Fact(DisplayName =
        "When an old episode of a show of unknown size is looked up outside the Index flow, FindEpisode reads only the newest page " +
        "(the free Paging.Total size probe) and matches from one title search, because search comes before any bounded walk.")]
    public async Task Old_release_uses_title_search_only()
    {
        // Arrange
        var title = "Lantern Harbour Night Stories With A Guest From The Archive";
        var released = DomainTestFixture.UtcDateDaysAgo(120);
        var match = Simple(_fixture.CreateSpotifyId(), title, released);
        StubTitleSearch([match]);
        var full = StubFullEpisode(new FullEpisode { Id = match.Id, Name = title, IsPlayable = true });
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(title, released), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(full);
        VerifyShowPage(Times.Once());
        VerifyTitleSearch(Times.Once());
    }

    [Fact(DisplayName =
        "When no Spotify show id is known, FindEpisode does not read a show feed and uses the title search only " +
        "because the shared candidate policy has no feed to read.")]
    public async Task No_show_id_uses_title_search_only()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(
            Request(released: DomainTestFixture.UtcDateDaysAgo(1), podcastSpotifyId: string.Empty), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeNull();
        VerifyShowPage(Times.Never());
        VerifyTitleSearch(Times.Once());
    }

    [Fact(DisplayName =
        "When neither the recent show page nor the title search matches, FindEpisode skips GetFullEpisode hydration " +
        "because there is no Spotify episode id to fetch.")]
    public async Task No_catalogue_match_skips_full_episode_hydration()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(released: DomainTestFixture.UtcDateDaysAgo(1)), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeNull();
        VerifyShowPage(Times.Once());
        VerifyTitleSearch(Times.Once());
        _mocker.GetMock<ISpotifyClientWrapper>().Verify(x => x.GetFullEpisode(
            It.IsAny<string>(), It.IsAny<EpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When two lookups for the same show run in one podcast pass, the recent show page is fetched once " +
        "because the pass cache serves the second lookup (docs/catalogue-pagination.md).")]
    public async Task Two_lookups_for_same_show_fetch_recent_page_once()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();
        var context = new IndexingContext();
        var showId = _fixture.CreateSpotifyId();
        StubShowPage([Simple(_fixture.CreateSpotifyId(), "Volcanic Origami Workshop", DomainTestFixture.UtcDateDaysAgo(1))]);

        // Act
        await sut.FindEpisode(Request(released: DomainTestFixture.UtcDateDaysAgo(1), podcastSpotifyId: showId), context);
        await sut.FindEpisode(Request(released: DomainTestFixture.UtcDateDaysAgo(2), podcastSpotifyId: showId), context);

        // Assert
        VerifyShowPage(Times.Once());
    }

    [Fact(DisplayName =
        "When the title-search page contains an in-band episode and a years-apart episode with the same title, FindEpisode matches the in-band one " +
        "because the release date is a short window, not a walk from that date to today.")]
    public async Task In_band_candidate_is_matched_before_years_apart_candidate()
    {
        // Arrange
        var title = "Lantern Harbour Night Stories With A Guest From The Archive";
        var released = DomainTestFixture.UtcDateDaysAgo(60);
        var yearsApart = Simple(_fixture.CreateSpotifyId(), title, released.AddYears(-3));
        var inBand = Simple(_fixture.CreateSpotifyId(), title, released);
        StubTitleSearch([yearsApart, inBand]);
        var full = StubFullEpisode(new FullEpisode { Id = inBand.Id, Name = title, IsPlayable = true });
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(title, released), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(full);
    }

    [Fact(DisplayName =
        "When no in-band title-search hit matches, FindEpisode tries the out-of-band hits on the same page " +
        "because a years-apart publishing lag is a weaker second pass and must not start a catalogue walk.")]
    public async Task Out_of_band_candidate_is_matched_only_after_in_band_miss()
    {
        // Arrange
        var title = "Lantern Harbour Night Stories With A Guest From The Archive";
        var released = DomainTestFixture.UtcDateDaysAgo(60);
        var yearsApart = Simple(_fixture.CreateSpotifyId(), title, released.AddYears(-3));
        var inBandOther = Simple(_fixture.CreateSpotifyId(), "Volcanic Origami Workshop", released);
        StubTitleSearch([inBandOther, yearsApart]);
        var full = StubFullEpisode(new FullEpisode { Id = yearsApart.Id, Name = title, IsPlayable = true });
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(title, released), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(full);
    }

    [Fact(DisplayName =
        "When a catalogue match hydrates to IsPlayable=false, FindEpisode returns no episode and the skip logger runs, " +
        "because catalogue lookups must still drop episodes that are not free.")]
    public async Task Catalogue_match_drops_non_playable_full_episode()
    {
        // Arrange
        var title = "Lantern Harbour Night Stories With A Guest From The Archive";
        var released = DomainTestFixture.UtcDateDaysAgo(1);
        var match = Simple(_fixture.CreateSpotifyId(), title, released);
        StubShowPage([match]);
        StubFullEpisode(new FullEpisode { Id = match.Id, Name = title, IsPlayable = false });
        var logger = _mocker.GetMock<ILogger<SpotifyEpisodeResolver>>();
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(title, released, market: Market.CountryCode), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeNull();
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v!.ToString()!.Contains("Skipping Spotify episode") &&
                    v!.ToString()!.Contains(match.Id)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When EpisodeSpotifyId is set and GetFullEpisode is unplayable with restrictions.reason=market, FindEpisode returns that episode " +
        "and logs that it was returned despite the GB market, because a known episode id is not a catalogue skip.")]
    public async Task Direct_episode_id_keeps_market_restricted_full_episode()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var restricted = new FullEpisodeWithRestrictions
        {
            Id = episodeId,
            Name = _fixture.CreateTitle(),
            IsPlayable = false,
            Restrictions = new Dictionary<string, string>
            {
                ["reason"] = SpotifyNonPlayableSkipLogger.MarketRestrictionReason
            }
        };
        var wrapper = _mocker.GetMock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetFullEpisode(
                episodeId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(restricted);
        var logger = _mocker.GetMock<ILogger<SpotifyEpisodeResolver>>();
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: episodeId,
            EpisodeTitle: _fixture.CreateTitle(),
            Released: DomainTestFixture.UtcDateDaysAgo(1),
            HasExpensiveSpotifyEpisodesQuery: false,
            Market: Market.CountryCode);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(restricted);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v!.ToString()!.Contains(SpotifyNonPlayableSkipLogger.ReturnedDespiteMarketMessagePrefix) &&
                    v!.ToString()!.Contains(episodeId) &&
                    v!.ToString()!.Contains($"market='{Market.CountryCode}'") &&
                    v!.ToString()!.Contains(
                        $"restrictions.reason='{SpotifyNonPlayableSkipLogger.MarketRestrictionReason}'")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v!.ToString()!.Contains("Skipping Spotify episode") ||
                    v!.ToString()!.Contains(SpotifyNonPlayableSkipLogger.MarketUnavailableMessagePrefix)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
        VerifyNoCatalogueLookup();
    }

    [Fact(DisplayName =
        "When EpisodeSpotifyId is set and GetFullEpisode is unplayable for a reason other than market, FindEpisode still returns that episode " +
        "and does not log a skip, because a known episode id is not a catalogue drop.")]
    public async Task Direct_episode_id_keeps_non_market_restricted_full_episode_without_skip_log()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var restricted = new FullEpisodeWithRestrictions
        {
            Id = episodeId,
            Name = _fixture.CreateTitle(),
            IsPlayable = false,
            Restrictions = new Dictionary<string, string> { ["reason"] = "product" }
        };
        var wrapper = _mocker.GetMock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetFullEpisode(
                episodeId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(restricted);
        var logger = _mocker.GetMock<ILogger<SpotifyEpisodeResolver>>();
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: episodeId,
            EpisodeTitle: _fixture.CreateTitle(),
            Released: DomainTestFixture.UtcDateDaysAgo(1),
            HasExpensiveSpotifyEpisodesQuery: false,
            Market: Market.CountryCode);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(restricted);
        logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v!.ToString()!.Contains(SpotifyNonPlayableSkipLogger.ReturnedDespiteMarketMessagePrefix) ||
                    v!.ToString()!.Contains("Skipping Spotify episode") ||
                    v!.ToString()!.Contains(SpotifyNonPlayableSkipLogger.MarketUnavailableMessagePrefix)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }


    [Fact(DisplayName =
        "When the podcast's stored Spotify count says the show is small (<=200), FindEpisode reads every page of the show and makes no " +
        "title search, because a full read of a small show is cheap and finds old episodes whose titles differ.")]
    public async Task Small_show_is_read_in_full_without_search()
    {
        // Arrange
        var title = "Lantern Harbour Night Stories With A Guest From The Archive";
        var released = DomainTestFixture.UtcDateDaysAgo(900);
        var catalogue = Catalogue(120, released.AddDays(1000));
        var oldest = Simple(_fixture.CreateSpotifyId(), title, released);
        catalogue[^1] = oldest;
        StubCatalogue(catalogue);
        var full = StubFullEpisode(new FullEpisode { Id = oldest.Id, Name = title, IsPlayable = true });
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(title, released, showEpisodeCount: 120), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(full);
        VerifyShowPage(Times.Exactly(3));
        VerifyTitleSearch(Times.Never());
    }

    [Fact(DisplayName =
        "When an old episode of a large show misses the title search, FindEpisode walks at most 4 newest pages and then misses cleanly, " +
        "because the bounded walk is the last resort and must stay bounded (pagination HARD rule).")]
    public async Task Large_show_old_episode_search_miss_walks_four_pages_then_misses()
    {
        // Arrange
        var released = DomainTestFixture.UtcDateDaysAgo(2000);
        StubCatalogue(Catalogue(600, DateTime.UtcNow.Date));
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(released: released, showEpisodeCount: 600), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeNull();
        VerifyTitleSearch(Times.Once());
        VerifyShowPage(Times.Exactly(new EpisodeLookupOptions().SpotifyBoundedWalkPages));
    }

    [Fact(DisplayName =
        "When the Index flow looks up an episode of a large show, FindEpisode reads newest pages until a page is older than the window " +
        "start and never searches, because the indexer's episodes are always near the top of the feed.")]
    public async Task Index_flow_reads_newest_pages_until_past_window_without_search()
    {
        // Arrange
        var released = DomainTestFixture.UtcDateDaysAgo(1);
        StubCatalogue(Catalogue(600, DateTime.UtcNow.Date)); // one episode a day: page 0 covers 50 days
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();
        var context = new IndexingContext(DateTime.UtcNow.AddDays(-60)) { LookupFlow = EpisodeLookupFlow.Index };

        // Act
        var result = await sut.FindEpisode(Request(released: released, showEpisodeCount: 600), context);

        // Assert
        result.FullEpisode.Should().BeNull();
        VerifyTitleSearch(Times.Never());
        VerifyShowPage(Times.Exactly(2));
    }

    [Fact(DisplayName =
        "When the indexer looks up an episode of a YouTube-only podcast with no Spotify show id (Narcissists Playbook, 13 Oct 2026), " +
        "FindEpisode makes no Spotify search or page call and returns no episode, because a show-name-only search cannot succeed " +
        "and was repeated every hour.")]
    public async Task Index_flow_without_show_id_makes_no_spotify_calls()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();
        var context = new IndexingContext(DateTime.UtcNow.AddDays(-3)) { LookupFlow = EpisodeLookupFlow.Index };
        var request = Request(
            "Narcissist's Playbook: People Need to Know They're Not Crazy",
            DomainTestFixture.UtcDateDaysAgo(2),
            podcastSpotifyId: string.Empty);

        // Act
        var result = await sut.FindEpisode(request, context);

        // Assert
        result.FullEpisode.Should().BeNull();
        VerifyNoCatalogueLookup();
    }

    [Fact(DisplayName =
        "When a URL submission looks up a YouTube episode of a podcast with no Spotify show id, FindEpisode still runs one title " +
        "search and returns the match, because submission is how a podcast's Spotify show can first be found.")]
    public async Task Submit_flow_without_show_id_still_title_searches()
    {
        // Arrange
        var title = "Narcissist's Playbook: People Need to Know They're Not Crazy";
        var released = DomainTestFixture.UtcDateDaysAgo(2);
        var match = Simple(_fixture.CreateSpotifyId(), title, released);
        StubTitleSearch([match]);
        var full = StubFullEpisode(new FullEpisode { Id = match.Id, Name = title, IsPlayable = true });
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();
        var context = new IndexingContext { LookupFlow = EpisodeLookupFlow.Submit };

        // Act
        var result = await sut.FindEpisode(Request(title, released, podcastSpotifyId: string.Empty), context);

        // Assert
        result.FullEpisode.Should().BeSameAs(full);
        VerifyTitleSearch(Times.Once());
        VerifyShowPage(Times.Never());
    }

    [Fact(DisplayName =
        "When every Spotify call times out (guard returns null), FindEpisode returns a clean miss instead of throwing, " +
        "because a slow catalogue must not fail the submit or index pass.")]
    public async Task Guarded_failures_return_clean_miss()
    {
        // Arrange
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(x => x.GetShowEpisodes(
                It.IsAny<string>(), It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());
        var sut = _mocker.CreateInstance<SpotifyEpisodeResolver>();

        // Act
        var result = await sut.FindEpisode(Request(released: DomainTestFixture.UtcDateDaysAgo(1)), new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeNull();
        VerifyTitleSearch(Times.Once());
    }

    private List<SimpleEpisode> Catalogue(int count, DateTime newest) =>
        Enumerable.Range(0, count)
            .Select(i => Simple(_fixture.CreateSpotifyId(), $"Unrelated Episode Number {i} About Volcanic Origami", newest.AddDays(-i),
                TimeSpan.FromMinutes(20)))
            .ToList();

    private void StubCatalogue(IReadOnlyList<SimpleEpisode> newestFirst) =>
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(x => x.GetShowEpisodes(
                It.IsAny<string>(), It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, ShowEpisodesRequest r, IndexingContext _, CancellationToken _) =>
                new Paging<SimpleEpisode>
                {
                    Items = newestFirst.Skip(r.Offset ?? 0).Take(r.Limit ?? 20).ToList(),
                    Total = newestFirst.Count
                });

    private static TitleSubstringSubjectClassifier BenMcKenzieSubjects() =>
        new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ben McKenzie"] = ["Cult Psychology", "Multi-Level Marketing"]
        });

    private FindSpotifyEpisodeRequest Request(
        string? title = null,
        DateTime? released = null,
        string? episodeSpotifyId = null,
        string? podcastSpotifyId = null,
        Service? releaseAuthority = null,
        TimeSpan? length = null,
        bool enrichingYouTubeDiscoveredEpisode = false,
        string? market = null,
        int? showEpisodeCount = null) =>
        new(
            PodcastSpotifyId: podcastSpotifyId ?? _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: episodeSpotifyId ?? string.Empty,
            EpisodeTitle: title ?? _fixture.CreateTitle(),
            Released: released ?? DomainTestFixture.UtcDateDaysAgo(1),
            HasExpensiveSpotifyEpisodesQuery: false,
            ReleaseAuthority: releaseAuthority,
            Length: length,
            Market: market,
            EnrichingYouTubeDiscoveredEpisode: enrichingYouTubeDiscoveredEpisode,
            ShowEpisodeCount: showEpisodeCount);

    private SimpleEpisode Simple(string id, string title, DateTime release, TimeSpan? length = null) =>
        new()
        {
            Id = id,
            Name = title,
            DurationMs = (int)(length ?? TimeSpan.FromMinutes(44)).TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
            Type = ItemType.Episode,
            IsPlayable = true
        };

    private void StubShowPage(IList<SimpleEpisode> episodes) =>
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(x => x.GetShowEpisodes(
                It.IsAny<string>(), It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paging<SimpleEpisode> { Items = episodes.ToList() });

    private void StubTitleSearch(IReadOnlyList<SimpleEpisode> episodes) =>
        _mocker.GetMock<ISpotifyEpisodeTitleSearch>()
            .Setup(x => x.FindCandidates(It.IsAny<FindSpotifyEpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

    private FullEpisode StubFullEpisode(FullEpisode episode)
    {
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(x => x.GetFullEpisode(
                episode.Id, It.IsAny<EpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);
        return episode;
    }

    private void VerifyShowPage(Times times) =>
        _mocker.GetMock<ISpotifyClientWrapper>().Verify(x => x.GetShowEpisodes(
            It.IsAny<string>(),
            It.Is<ShowEpisodesRequest>(r => r.Limit == SpotifyShowEpisodePages.PageSize),
            It.IsAny<IndexingContext>(),
            It.IsAny<CancellationToken>()), times);

    private void VerifyTitleSearch(Times times) =>
        _mocker.GetMock<ISpotifyEpisodeTitleSearch>().Verify(x => x.FindCandidates(
            It.IsAny<FindSpotifyEpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), times);

    private void VerifyNoCatalogueLookup()
    {
        VerifyShowPage(Times.Never());
        VerifyTitleSearch(Times.Never());
    }
}
