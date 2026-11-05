using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.News;
using Api.Services.Podcasts;
using Api.Services.TvShows;
using Api.Services.YouTube;
using Azure.Search.Documents;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.YouTube;

/// <summary>
/// When a playlist PATCH validates successfully and the parent's channel is blank, persist OwnerChannelId so
/// discovery / URL routing can match by channel. A non-blank intentional channel is kept; channel mismatch still refuses.
/// </summary>
public class ParentEditPlaylistOwnerChannelRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();

    public ParentEditPlaylistOwnerChannelRules()
    {
        _mocker.Use(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SearchClient)) as SearchClient);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelAgainstPlaylist(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((YouTubePlaylistValidation?)null);
    }

    private void GuardReturnsValid(string playlistId, string ownerChannelId) =>
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.Check(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(
                YouTubePlaylistValidationStatus.Valid, playlistId, ownerChannelId));

    [Fact(DisplayName =
        "PATCH podcast with a new playlist and blank channel persists the playlist OwnerChannelId onto youTubeChannelId so discovery can match the parent.")]
    public async Task podcast_playlist_save_with_blank_channel_derives_owner()
    {
        // Arrange
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = string.Empty;
        });
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        GuardReturnsValid(requestedPlaylist, ownerChannelId);

        // Act
        var result = await _mocker.CreateInstance<PodcastUpdateService>().UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { YouTubePlaylistId = requestedPlaylist }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        podcast.YouTubePlaylistId.Should().Be(requestedPlaylist);
        podcast.YouTubeChannelId.Should().Be(ownerChannelId);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(podcast), Times.Once);
    }

    [Fact(DisplayName =
        "PATCH podcast with a new playlist leaves a non-blank intentional channel unchanged, even when it is not the playlist owner.")]
    public async Task podcast_playlist_save_keeps_non_blank_channel()
    {
        // Arrange
        var storedChannelId = _fixture.CreateYouTubeChannelId();
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = storedChannelId;
            p.YouTubePlaylistId = string.Empty;
        });
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        GuardReturnsValid(requestedPlaylist, ownerChannelId);

        // Act
        var result = await _mocker.CreateInstance<PodcastUpdateService>().UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { YouTubePlaylistId = requestedPlaylist }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        podcast.YouTubePlaylistId.Should().Be(requestedPlaylist);
        podcast.YouTubeChannelId.Should().Be(storedChannelId);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(podcast), Times.Once);
    }

    [Fact(DisplayName =
        "PATCH podcast youTubeChannelId that does not own the stored playlist is still refused with youTubePlaylistChannelMismatch and nothing is saved.")]
    public async Task podcast_channel_mismatch_still_refused()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var requestedChannelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = playlistId;
        });
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.Check(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((YouTubePlaylistValidation?)null);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelAgainstPlaylist(
                playlistId, null, string.Empty, requestedChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(
                YouTubePlaylistValidationStatus.ChannelMismatch, playlistId, ownerChannelId));

        // Act
        var result = await _mocker.CreateInstance<PodcastUpdateService>().UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { YouTubeChannelId = requestedChannelId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.BadRequest);
        result.Code.Should().Be(YouTubePlaylistValidation.ChannelMismatchCode);
        podcast.YouTubeChannelId.Should().BeEmpty();
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
    }

    [Fact(DisplayName =
        "PATCH news organisation with a new playlist and blank channel persists OwnerChannelId onto indexing.youTubeChannelId.")]
    public async Task news_organisation_playlist_save_with_blank_channel_derives_owner()
    {
        // Arrange
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid()
        };
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        GuardReturnsValid(requestedPlaylist, ownerChannelId);

        // Act
        var result = await _mocker.CreateInstance<NewsOrganisationService>().UpdateAsync(
            new NewsOrganisationChangeRequestWrapper(organisation.Id, new NewsOrganisationChangeRequest
            {
                YouTubePlaylistId = requestedPlaylist
            }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing.Should().NotBeNull();
        organisation.Indexing!.YouTubePlaylistId.Should().Be(requestedPlaylist);
        organisation.Indexing.YouTubeChannelId.Should().Be(ownerChannelId);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Save(organisation), Times.Once);
    }

    [Fact(DisplayName =
        "PATCH TV show with a new playlist and blank channel persists OwnerChannelId onto indexing.youTubeChannelId.")]
    public async Task tv_show_playlist_save_with_blank_channel_derives_owner()
    {
        // Arrange
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid()
        };
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        GuardReturnsValid(requestedPlaylist, ownerChannelId);

        // Act
        var result = await _mocker.CreateInstance<TvShowService>().UpdateAsync(
            new TvShowChangeRequestWrapper(show.Id, new TvShowChangeRequest { YouTubePlaylistId = requestedPlaylist }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        show.Indexing.Should().NotBeNull();
        show.Indexing!.YouTubePlaylistId.Should().Be(requestedPlaylist);
        show.Indexing.YouTubeChannelId.Should().Be(ownerChannelId);
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.Save(show), Times.Once);
    }

    [Fact(DisplayName =
        "PATCH news organisation with a new playlist leaves a non-blank indexing channel unchanged.")]
    public async Task news_organisation_playlist_save_keeps_non_blank_channel()
    {
        // Arrange
        var storedChannelId = _fixture.CreateYouTubeChannelId();
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = storedChannelId }
        };
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        GuardReturnsValid(requestedPlaylist, ownerChannelId);

        // Act
        var result = await _mocker.CreateInstance<NewsOrganisationService>().UpdateAsync(
            new NewsOrganisationChangeRequestWrapper(organisation.Id, new NewsOrganisationChangeRequest
            {
                YouTubePlaylistId = requestedPlaylist
            }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing!.YouTubePlaylistId.Should().Be(requestedPlaylist);
        organisation.Indexing.YouTubeChannelId.Should().Be(storedChannelId);
    }

    [Fact(DisplayName =
        "PATCH TV show with a new playlist leaves a non-blank indexing channel unchanged.")]
    public async Task tv_show_playlist_save_keeps_non_blank_channel()
    {
        // Arrange
        var storedChannelId = _fixture.CreateYouTubeChannelId();
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = storedChannelId }
        };
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        GuardReturnsValid(requestedPlaylist, ownerChannelId);

        // Act
        var result = await _mocker.CreateInstance<TvShowService>().UpdateAsync(
            new TvShowChangeRequestWrapper(show.Id, new TvShowChangeRequest { YouTubePlaylistId = requestedPlaylist }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        show.Indexing!.YouTubePlaylistId.Should().Be(requestedPlaylist);
        show.Indexing.YouTubeChannelId.Should().Be(storedChannelId);
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.Save(show), Times.Once);
    }

    [Fact(DisplayName =
        "PATCH news organisation with a Valid playlist and blank channel skips derive when the owner channel is already taken, still saves the playlist, and leaves the channel blank.")]
    public async Task news_organisation_playlist_save_skips_derive_when_owner_channel_taken()
    {
        // Arrange
        var requestedPlaylist = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var otherOwnerId = _fixture.CreateGuid();
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid()
        };
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        GuardReturnsValid(requestedPlaylist, ownerChannelId);
        _mocker.GetMock<IServiceIdsUniqueness>()
            .Setup(u => u.FindConflict(
                organisation.Id,
                It.Is<ServiceIdsChange>(c => c.YouTubeChannelId == ownerChannelId)))
            .ReturnsAsync(new ServiceIdConflict("youTubeChannelId", ownerChannelId, [otherOwnerId]));

        // Act
        var result = await _mocker.CreateInstance<NewsOrganisationService>().UpdateAsync(
            new NewsOrganisationChangeRequestWrapper(organisation.Id, new NewsOrganisationChangeRequest
            {
                YouTubePlaylistId = requestedPlaylist
            }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing.Should().NotBeNull();
        organisation.Indexing!.YouTubePlaylistId.Should().Be(requestedPlaylist);
        organisation.Indexing.YouTubeChannelId.Should().BeNullOrEmpty();
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Save(organisation), Times.Once);
    }
}
