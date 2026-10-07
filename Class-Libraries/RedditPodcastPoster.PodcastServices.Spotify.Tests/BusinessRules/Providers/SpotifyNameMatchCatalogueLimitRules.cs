using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Providers;

/// <summary>
/// A single-episode Spotify name match must stay inside a release window.
/// Missing both the indexing window and the episode release must not download the show.
/// </summary>
public class SpotifyNameMatchCatalogueLimitRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public SpotifyNameMatchCatalogueLimitRules()
    {
        _mocker.Use(NullLogger<SpotifyPodcastEpisodesProvider>.Instance);
    }

    [Fact(DisplayName =
        "When a name match has neither ReleasedSince nor an episode release, GetAllEpisodes does not call Spotify " +
        "because a single-episode lookup must not download the show catalogue.")]
    public async Task Name_match_without_any_release_does_not_call_spotify()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SpotifyPodcastEpisodesProvider>();
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: string.Empty,
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: string.Empty,
            EpisodeTitle: _fixture.CreateTitle(),
            Released: null,
            HasExpensiveSpotifyEpisodesQuery: true);

        // Act
        var result = await sut.GetAllEpisodes(
            request,
            new IndexingContext(SkipPodcastDiscovery: false, SkipExpensiveSpotifyQueries: false),
            Market.CountryCode);

        // Assert
        result.Episodes.Should().BeEmpty();
        _mocker.GetMock<ISpotifyClientWrapper>().Verify(
            x => x.GetSimpleShows(It.IsAny<SearchRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mocker.GetMock<ISpotifyQueryPaginator>().Verify(
            x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When a name match has an episode release but no indexing ReleasedSince, GetAllEpisodes paginates from that release " +
        "because the Spotify walk must stay near the episode instead of downloading the catalogue.")]
    public async Task Name_match_uses_episode_release_when_indexing_window_is_missing()
    {
        // Arrange
        var showId = _fixture.CreateSpotifyId();
        var showName = _fixture.CreateTitle();
        var show = new SimpleShow { Id = showId, Name = showName };
        var episodeRelease = DomainTestFixture.UtcDateDaysAgo(0);
        IndexingContext? capturedFetchContext = null;

        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(x => x.GetSimpleShows(It.IsAny<SearchRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([show]);
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(x => x.GetShowEpisodes(
                showId,
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paging<SimpleEpisode> { Items = [] });
        _mocker.GetMock<ISpotifySearchResultFinder>()
            .Setup(x => x.FindMatchingPodcasts(showName, It.IsAny<List<SimpleShow>?>()))
            .Returns([show]);
        _mocker.GetMock<ISpotifyQueryPaginator>()
            .Setup(x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()))
            .Callback<IPaginatable<SimpleEpisode>?, IndexingContext>((_, ctx) => capturedFetchContext = ctx)
            .ReturnsAsync(new PodcastEpisodesResult([]));

        var sut = _mocker.CreateInstance<SpotifyPodcastEpisodesProvider>();
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: string.Empty,
            PodcastName: showName,
            EpisodeSpotifyId: string.Empty,
            EpisodeTitle: _fixture.CreateTitle(),
            Released: episodeRelease,
            HasExpensiveSpotifyEpisodesQuery: true);

        // Act
        await sut.GetAllEpisodes(
            request,
            new IndexingContext(SkipPodcastDiscovery: false, SkipExpensiveSpotifyQueries: false),
            Market.CountryCode);

        // Assert
        capturedFetchContext.Should().NotBeNull();
        capturedFetchContext!.ReleasedSince.Should().Be(
            EpisodeReleaseTolerance.GetSpotifyCatalogueFetchReleasedSince(episodeRelease));
    }
}
