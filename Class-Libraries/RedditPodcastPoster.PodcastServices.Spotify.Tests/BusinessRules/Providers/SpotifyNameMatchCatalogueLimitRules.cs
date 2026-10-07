using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Providers;

/// <summary>
/// A single-episode Spotify name match must not download the show catalogue.
/// An unset indexing ReleasedSince refuses the walk even when the episode has a release date.
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
        "When a name match has a years-old episode release but no indexing ReleasedSince, GetAllEpisodes does not call Spotify " +
        "because that date must not become a catalogue floor walked from today.")]
    public async Task Years_old_episode_release_without_indexing_window_does_not_call_spotify()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SpotifyPodcastEpisodesProvider>();
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: string.Empty,
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: string.Empty,
            EpisodeTitle: _fixture.CreateTitle(),
            Released: DomainTestFixture.UtcDateDaysAgo(800),
            HasExpensiveSpotifyEpisodesQuery: false);

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
        _mocker.GetMock<ISpotifyClientWrapper>().Verify(
            x => x.GetShowEpisodes(
                It.IsAny<string>(),
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mocker.GetMock<ISpotifyQueryPaginator>().Verify(
            x => x.PaginateEpisodes(It.IsAny<IPaginatable<SimpleEpisode>?>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }
}
