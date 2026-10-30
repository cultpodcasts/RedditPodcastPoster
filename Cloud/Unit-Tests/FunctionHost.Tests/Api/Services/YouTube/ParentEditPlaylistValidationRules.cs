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
/// A podcast, news organisation or TV show must not be saved with a malformed, missing or private YouTube playlist: the
/// playlist is the parent's only YouTube source, so it would index nothing. Those are a 400 with a stable code; a lookup
/// that could not be made is a retryable 503. Nothing is saved. Which channel owns the playlist does not matter.
/// </summary>
public class ParentEditPlaylistValidationRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();

    private void GuardRefuses(YouTubePlaylistValidationStatus status) =>
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.Check(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(status, "PLsyntheticplaylist0001"));

    [Theory(DisplayName =
        "PATCH podcast with a malformed or missing playlist is a BadRequest with the playlist code; a failed lookup is ServiceUnavailable; nothing is saved.")]
    [InlineData(YouTubePlaylistValidationStatus.InvalidPlaylistId, PodcastUpdateStatus.BadRequest, "youTubePlaylistIdInvalid")]
    [InlineData(YouTubePlaylistValidationStatus.PlaylistNotFound, PodcastUpdateStatus.BadRequest, "youTubePlaylistNotFound")]
    [InlineData(YouTubePlaylistValidationStatus.LookupFailed, PodcastUpdateStatus.ServiceUnavailable, "youTubePlaylistLookupFailed")]
    public async Task podcast_edit_refused(YouTubePlaylistValidationStatus status, PodcastUpdateStatus expected, string code)
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
        _mocker.GetMock<IYouTubePlaylistGuard>().Verify(g =>
            g.Check(playlistId, requested, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName =
        "PATCH news organisation with a bad YouTube playlist is refused with the playlist code and not saved; only the playlist is passed to the guard.")]
    [InlineData(YouTubePlaylistValidationStatus.PlaylistNotFound, NewsOrganisationUpdateStatus.BadRequest, "youTubePlaylistNotFound")]
    [InlineData(YouTubePlaylistValidationStatus.LookupFailed, NewsOrganisationUpdateStatus.ServiceUnavailable, "youTubePlaylistLookupFailed")]
    public async Task news_organisation_edit_refused(YouTubePlaylistValidationStatus status,
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
        _mocker.GetMock<IYouTubePlaylistGuard>().Verify(g =>
            g.Check(null, requestedPlaylist, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName =
        "PATCH TV show with a bad YouTube playlist is refused with the playlist code and not saved.")]
    [InlineData(YouTubePlaylistValidationStatus.InvalidPlaylistId, TvShowUpdateStatus.BadRequest, "youTubePlaylistIdInvalid")]
    [InlineData(YouTubePlaylistValidationStatus.LookupFailed, TvShowUpdateStatus.ServiceUnavailable, "youTubePlaylistLookupFailed")]
    public async Task tv_show_edit_refused(YouTubePlaylistValidationStatus status, TvShowUpdateStatus expected, string code)
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
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
            new TvShowChangeRequestWrapper(show.Id, new TvShowChangeRequest { YouTubePlaylistId = requestedPlaylist }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(expected);
        result.Code.Should().Be(code);
        show.Indexing!.YouTubePlaylistId.Should().Be(playlistId);
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.Save(It.IsAny<TvShow>()), Times.Never);
        _mocker.GetMock<IYouTubePlaylistGuard>().Verify(g =>
            g.Check(playlistId, requestedPlaylist, It.IsAny<CancellationToken>()), Times.Once);
    }
}
