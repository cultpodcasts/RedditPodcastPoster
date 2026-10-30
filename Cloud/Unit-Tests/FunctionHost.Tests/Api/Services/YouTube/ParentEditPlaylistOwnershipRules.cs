using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;
using Api.Services.Podcasts;
using Api.Services.TvShows;
using Api.Services.YouTube;
using Azure.Search.Documents;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.YouTube;

/// <summary>
/// A podcast, news organisation or TV show must not be saved with a YouTube playlist its YouTube channel does not own:
/// the indexer keeps only the channel's own videos from the playlist, so it would index nothing. A mismatch (or a
/// missing playlist) is a 400 with a stable code; a lookup that could not be made is a retryable 503. Nothing is saved.
/// </summary>
public class ParentEditPlaylistOwnershipRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();

    private void GuardRefuses(YouTubePlaylistOwnershipStatus status) =>
        _mocker.GetMock<IYouTubePlaylistOwnershipGuard>()
            .Setup(g => g.Check(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistOwnership(status, "UCsynthetic", "PLsynthetic", "UCsyntheticowner"));

    [Theory(DisplayName =
        "PATCH podcast with a playlist another channel owns (or a missing playlist) is a BadRequest with the ownership code; a failed lookup is ServiceUnavailable; nothing is saved.")]
    [InlineData(YouTubePlaylistOwnershipStatus.Mismatch, PodcastUpdateStatus.BadRequest, "youTubePlaylistChannelMismatch")]
    [InlineData(YouTubePlaylistOwnershipStatus.PlaylistNotFound, PodcastUpdateStatus.BadRequest, "youTubePlaylistNotFound")]
    [InlineData(YouTubePlaylistOwnershipStatus.LookupFailed, PodcastUpdateStatus.ServiceUnavailable, "youTubePlaylistOwnerLookupFailed")]
    public async Task podcast_edit_refused(YouTubePlaylistOwnershipStatus status, PodcastUpdateStatus expected, string code)
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var requested = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = playlistId;
        });
        _mocker.Use(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SearchClient)) as SearchClient);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        GuardRefuses(status);

        // Act
        var result = await _mocker.CreateInstance<PodcastUpdateService>().UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { YouTubePlaylistId = requested }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(expected);
        result.Code.Should().Be(code);
        result.Error.Should().NotBeNullOrWhiteSpace();
        podcast.YouTubePlaylistId.Should().Be(playlistId);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
        _mocker.GetMock<IYouTubePlaylistOwnershipGuard>().Verify(g =>
            g.Check(channelId, playlistId, null, requested, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName =
        "PATCH news organisation with a YouTube channel/playlist pair that does not match is refused with the ownership code and not saved.")]
    [InlineData(YouTubePlaylistOwnershipStatus.Mismatch, NewsOrganisationUpdateStatus.BadRequest, "youTubePlaylistChannelMismatch")]
    [InlineData(YouTubePlaylistOwnershipStatus.LookupFailed, NewsOrganisationUpdateStatus.ServiceUnavailable, "youTubePlaylistOwnerLookupFailed")]
    public async Task news_organisation_edit_refused(YouTubePlaylistOwnershipStatus status,
        NewsOrganisationUpdateStatus expected, string code)
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var requestedChannel = _fixture.CreateYouTubeChannelId();
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
        var organisation = new NewsOrganisation("Synthetic News")
        {
            Id = _fixture.CreateGuid(),
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = channelId }
        };
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        GuardRefuses(status);

        // Act
        var result = await _mocker.CreateInstance<NewsOrganisationService>().UpdateAsync(
            new NewsOrganisationChangeRequestWrapper(organisation.Id, new NewsOrganisationChangeRequest
            {
                YouTubeChannelId = requestedChannel,
                YouTubePlaylistId = requestedPlaylist
            }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(expected);
        result.Code.Should().Be(code);
        organisation.Indexing!.YouTubeChannelId.Should().Be(channelId);
        organisation.Indexing.YouTubePlaylistId.Should().BeNull();
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Save(It.IsAny<NewsOrganisation>()), Times.Never);
        _mocker.GetMock<IYouTubePlaylistOwnershipGuard>().Verify(g =>
            g.Check(channelId, null, requestedChannel, requestedPlaylist, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName =
        "PATCH TV show with a YouTube channel/playlist pair that does not match is refused with the ownership code and not saved.")]
    [InlineData(YouTubePlaylistOwnershipStatus.Mismatch, TvShowUpdateStatus.BadRequest, "youTubePlaylistChannelMismatch")]
    [InlineData(YouTubePlaylistOwnershipStatus.LookupFailed, TvShowUpdateStatus.ServiceUnavailable, "youTubePlaylistOwnerLookupFailed")]
    public async Task tv_show_edit_refused(YouTubePlaylistOwnershipStatus status, TvShowUpdateStatus expected, string code)
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var requestedChannel = _fixture.CreateYouTubeChannelId();
        var show = new TvShow("Synthetic Docuseries")
        {
            Id = _fixture.CreateGuid(),
            Indexing = new CatalogueIndexingSource { YouTubePlaylistId = playlistId }
        };
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        GuardRefuses(status);

        // Act
        var result = await _mocker.CreateInstance<TvShowService>().UpdateAsync(
            new TvShowChangeRequestWrapper(show.Id, new TvShowChangeRequest { YouTubeChannelId = requestedChannel }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(expected);
        result.Code.Should().Be(code);
        show.Indexing!.YouTubeChannelId.Should().BeNull();
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.Save(It.IsAny<TvShow>()), Times.Never);
        _mocker.GetMock<IYouTubePlaylistOwnershipGuard>().Verify(g =>
            g.Check(null, playlistId, requestedChannel, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
