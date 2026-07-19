using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Providers;

/// <summary>
/// Name-match episode fetch must use a normal Spotify page size and aggregate all unique episodes.
/// Limit=1 plus FirstOrDefault aggregation caused discovery curation hangs / truncated results.
/// </summary>
public class SpotifyPodcastEpisodesProviderRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When resolving by podcast name with ReleasedSince, GetShowEpisodes uses Limit=50 " +
        "because Limit=1 walks one HTTP call per episode and times out discovery curation.")]
    public async Task Name_path_uses_spotify_page_size_when_released_since_set()
    {
        // Arrange
        ShowEpisodesRequest? capturedRequest = null;
        var show = new SimpleShow { Id = "show-1", Name = "News Hour" };
        var episode = CreateEpisode("ep-1", daysAgo: 1);
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetSimpleShows(It.IsAny<SearchRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([show]);
        wrapper
            .Setup(x => x.GetShowEpisodes(
                show.Id,
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ShowEpisodesRequest, IndexingContext, CancellationToken>((_, request, _, _) =>
                capturedRequest = request)
            .ReturnsAsync(new Paging<SimpleEpisode> { Items = [episode], Next = null });

        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingPodcasts(It.IsAny<string>(), It.IsAny<List<SimpleShow>?>()))
            .Returns([show]);

        var paginator = new Mock<ISpotifyQueryPaginator>();
        paginator
            .Setup(x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new PodcastEpisodesResult([episode]));

        var sut = CreateSut(wrapper.Object, paginator.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: "",
            PodcastName: "News Hour",
            EpisodeSpotifyId: "",
            EpisodeTitle: "Today",
            Released: DateTime.UtcNow.Date.AddDays(-1),
            HasExpensiveSpotifyEpisodesQuery: false);
        var indexingContext = new IndexingContext(
            ReleasedSince: DateTime.UtcNow.Date.AddDays(-2),
            SkipPodcastDiscovery: false);

        // Act
        await sut.GetAllEpisodes(request, indexingContext, Market.CountryCode);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Limit.Should().Be(50);
    }

    [Fact(DisplayName =
        "When name-path pagination returns multiple distinct episode ids, GetAllEpisodes returns all of them " +
        "because GroupBy.FirstOrDefault kept only the first id group and dropped the rest.")]
    public async Task Name_path_aggregates_all_unique_episodes_not_first_group_only()
    {
        // Arrange
        var show = new SimpleShow { Id = "show-1", Name = "News Hour" };
        var episode1 = CreateEpisode("ep-1", daysAgo: 1);
        var episode2 = CreateEpisode("ep-2", daysAgo: 0);
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetSimpleShows(It.IsAny<SearchRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([show]);
        wrapper
            .Setup(x => x.GetShowEpisodes(
                show.Id,
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paging<SimpleEpisode> { Items = [episode1, episode2], Next = null });

        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingPodcasts(It.IsAny<string>(), It.IsAny<List<SimpleShow>?>()))
            .Returns([show]);

        var paginator = new Mock<ISpotifyQueryPaginator>();
        paginator
            .Setup(x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new PodcastEpisodesResult([episode1, episode2]));

        var sut = CreateSut(wrapper.Object, paginator.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: "",
            PodcastName: "News Hour",
            EpisodeSpotifyId: "",
            EpisodeTitle: "Today",
            Released: DateTime.UtcNow.Date,
            HasExpensiveSpotifyEpisodesQuery: false);
        var indexingContext = new IndexingContext(SkipPodcastDiscovery: false);

        // Act
        var result = await sut.GetAllEpisodes(request, indexingContext, Market.CountryCode);

        // Assert
        result.Episodes.Select(x => x.Id).Should().BeEquivalentTo(episode1.Id, episode2.Id);
    }

    [Fact(DisplayName =
        "When resolving by known Spotify show id with ReleasedSince, GetShowEpisodes uses Limit=5 " +
        "because the indexing known-id path page size must stay bounded without using Limit=1.")]
    public async Task Known_id_path_uses_limit_five_when_released_since_set()
    {
        // Arrange
        ShowEpisodesRequest? capturedRequest = null;
        var showId = _fixture.CreateSpotifyId();
        var episode = CreateEpisode(_fixture.CreateSpotifyId(), daysAgo: 1);
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetShowEpisodes(
                showId,
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ShowEpisodesRequest, IndexingContext, CancellationToken>((_, request, _, _) =>
                capturedRequest = request)
            .ReturnsAsync(new Paging<SimpleEpisode> { Items = [episode], Next = null });

        var paginator = new Mock<ISpotifyQueryPaginator>();
        paginator
            .Setup(x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new PodcastEpisodesResult([episode]));

        var sut = CreateSut(wrapper.Object, paginator.Object, Mock.Of<ISpotifySearchResultFinder>());
        var indexingContext = new IndexingContext(
            ReleasedSince: DomainTestFixture.UtcDateDaysAgo(2),
            SkipPodcastDiscovery: true);

        // Act
        await sut.GetEpisodes(
            new GetEpisodesRequest(new SpotifyPodcastId(showId), Market.CountryCode),
            indexingContext);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Limit.Should().Be(5);
        wrapper.Verify(
            x => x.GetSimpleShows(It.IsAny<SearchRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When SkipExpensiveSpotifyQueries is set and the podcast has an expensive Spotify query, name-path GetAllEpisodes skips pagination " +
        "because indexer secondary passes and guarded submit must not walk high-volume catalogues by podcast name.")]
    public async Task Name_path_skips_pagination_when_expensive_query_guard_set()
    {
        // Arrange
        var show = new SimpleShow { Id = _fixture.CreateSpotifyId(), Name = _fixture.CreateTitle() };
        var episode = CreateEpisode(_fixture.CreateSpotifyId(), daysAgo: 1);
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetSimpleShows(It.IsAny<SearchRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([show]);
        wrapper
            .Setup(x => x.GetShowEpisodes(
                show.Id,
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paging<SimpleEpisode> { Items = [episode], Next = "https://api.spotify.com/v1/shows/x/episodes?offset=1" });

        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingPodcasts(It.IsAny<string>(), It.IsAny<List<SimpleShow>?>()))
            .Returns([show]);

        var paginator = new Mock<ISpotifyQueryPaginator>();
        var sut = CreateSut(wrapper.Object, paginator.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: "",
            PodcastName: show.Name,
            EpisodeSpotifyId: "",
            EpisodeTitle: _fixture.CreateTitle(),
            Released: DomainTestFixture.UtcDateDaysAgo(1),
            HasExpensiveSpotifyEpisodesQuery: true);
        var indexingContext = new IndexingContext(
            ReleasedSince: DomainTestFixture.UtcDateDaysAgo(2),
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: true);

        // Act
        var result = await sut.GetAllEpisodes(request, indexingContext, Market.CountryCode);

        // Assert
        result.Episodes.Should().BeEmpty();
        paginator.Verify(
            x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When SkipPodcastDiscovery is set and PodcastSpotifyId is empty, GetAllEpisodes does not name-search or fetch show episodes " +
        "because hourly indexing keeps discovery off and must never fall into MatchOtherServices-style show search.")]
    public async Task Empty_spotify_id_with_skip_podcast_discovery_does_not_name_search()
    {
        // Arrange
        var wrapper = new Mock<ISpotifyClientWrapper>();
        var finder = new Mock<ISpotifySearchResultFinder>();
        var paginator = new Mock<ISpotifyQueryPaginator>();
        var sut = CreateSut(wrapper.Object, paginator.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: "",
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: "",
            EpisodeTitle: _fixture.CreateTitle(),
            Released: DomainTestFixture.UtcDateDaysAgo(1),
            HasExpensiveSpotifyEpisodesQuery: true);
        var indexingContext = new IndexingContext(
            ReleasedSince: DomainTestFixture.UtcDateDaysAgo(7),
            SkipPodcastDiscovery: true,
            SkipExpensiveSpotifyQueries: true);

        // Act
        var result = await sut.GetAllEpisodes(request, indexingContext, Market.CountryCode);

        // Assert
        result.Episodes.Should().BeEmpty();
        wrapper.Verify(
            x => x.GetSimpleShows(It.IsAny<SearchRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        wrapper.Verify(
            x => x.GetShowEpisodes(
                It.IsAny<string>(),
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        paginator.Verify(
            x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When SkipExpensiveSpotifyQueries is set and the known-id podcast has an expensive Spotify query, GetEpisodes returns only the first page " +
        "because indexer secondary passes must not PaginateEpisodes over high-volume catalogues.")]
    public async Task Known_id_path_returns_first_page_only_when_expensive_query_guard_set()
    {
        // Arrange
        var showId = _fixture.CreateSpotifyId();
        var episode = CreateEpisode(_fixture.CreateSpotifyId(), daysAgo: 1);
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetShowEpisodes(
                showId,
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paging<SimpleEpisode>
            {
                Items = [episode],
                Next = "https://api.spotify.com/v1/shows/x/episodes?offset=5"
            });

        var paginator = new Mock<ISpotifyQueryPaginator>();
        var sut = CreateSut(wrapper.Object, paginator.Object, Mock.Of<ISpotifySearchResultFinder>());
        var indexingContext = new IndexingContext(
            ReleasedSince: DomainTestFixture.UtcDateDaysAgo(2),
            SkipPodcastDiscovery: true,
            SkipExpensiveSpotifyQueries: true);

        // Act
        var result = await sut.GetEpisodes(
            new GetEpisodesRequest(new SpotifyPodcastId(showId), Market.CountryCode, HasExpensiveSpotifyEpisodesQuery: true),
            indexingContext);

        // Assert
        result.Episodes.Select(x => x.Id).Should().ContainSingle().Which.Should().Be(episode.Id);
        paginator.Verify(
            x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the first page includes a non-playable episode, GetEpisodes excludes it under the expensive-query guard " +
        "because paywalled Spotify episodes must not enter the catalogue matching cache.")]
    public async Task Known_id_path_excludes_non_playable_on_first_page()
    {
        // Arrange
        var showId = _fixture.CreateSpotifyId();
        var free = CreateEpisode(_fixture.CreateSpotifyId(), daysAgo: 1, isPlayable: true);
        var paywalled = CreateEpisode(_fixture.CreateSpotifyId(), daysAgo: 1, isPlayable: false);
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetShowEpisodes(
                showId,
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paging<SimpleEpisode>
            {
                Items = [free, paywalled],
                Next = "https://api.spotify.com/v1/shows/x/episodes?offset=5"
            });

        var sut = CreateSut(wrapper.Object, Mock.Of<ISpotifyQueryPaginator>(), Mock.Of<ISpotifySearchResultFinder>());
        var indexingContext = new IndexingContext(
            ReleasedSince: DomainTestFixture.UtcDateDaysAgo(2),
            SkipPodcastDiscovery: true,
            SkipExpensiveSpotifyQueries: true);

        // Act
        var result = await sut.GetEpisodes(
            new GetEpisodesRequest(new SpotifyPodcastId(showId), Market.CountryCode, HasExpensiveSpotifyEpisodesQuery: true),
            indexingContext);

        // Assert
        result.Episodes.Select(x => x.Id).Should().ContainSingle().Which.Should().Be(free.Id);
    }

    private static SpotifyPodcastEpisodesProvider CreateSut(
        ISpotifyClientWrapper wrapper,
        ISpotifyQueryPaginator paginator,
        ISpotifySearchResultFinder finder) =>
        new(wrapper, paginator, finder, NullLogger<SpotifyPodcastEpisodesProvider>.Instance);

    private SimpleEpisode CreateEpisode(string id, int daysAgo, bool isPlayable = true) =>
        new()
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(daysAgo).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode,
            IsPlayable = isPlayable
        };
}
