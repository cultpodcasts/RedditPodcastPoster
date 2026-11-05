using System.Linq.Expressions;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.News;
using Api.Services.Podcasts;
using Api.Services.TvShows;
using Api.Services.YouTube;
using Azure.Search.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.YouTube;

/// <summary>
/// Save-time rule (#1180): a channel-wide YouTube claim (channel id, blank playlist) is refused with
/// <c>channelWideYouTubeConflict</c> when a live claimant of another kind already claims that channel channel-wide.
/// Playlist-scoped parents may share a channel; removed claimants are ignored; clearing a playlist that would create
/// the clash is also refused.
/// </summary>
public class ChannelWideYouTubeClaimRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<Podcast> _podcasts = [];
    private Podcast? _savedPodcast;
    private NewsOrganisation? _savedOrganisation;
    private TvShow? _savedShow;

    public ChannelWideYouTubeClaimRules()
    {
        _mocker.Use(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SearchClient)) as SearchClient);
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(NullLogger<PodcastUpdateService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.Check(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((YouTubePlaylistValidation?)null);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelAgainstPlaylist(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((YouTubePlaylistValidation?)null);

        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> match) =>
                _organisations.Where(match.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>(),
                It.IsAny<Expression<Func<NewsOrganisation, Guid>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> match,
                Expression<Func<NewsOrganisation, Guid>> project) =>
                _organisations.Where(match.Compile()).Select(project.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> match) =>
                _shows.Where(match.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>(),
                It.IsAny<Expression<Func<TvShow, Guid>>>()))
            .Returns((Expression<Func<TvShow, bool>> match, Expression<Func<TvShow, Guid>> project) =>
                _shows.Where(match.Compile()).Select(project.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> match) =>
                _podcasts.Where(match.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.Save(It.IsAny<Podcast>()))
            .Callback<Podcast>(p => _savedPodcast = p)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(o => _savedOrganisation = o)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(s => _savedShow = s)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns(AsyncEnumerable.Empty<NewsReport>());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns(AsyncEnumerable.Empty<TvShowEpisode>());

        _mocker.Use<IChannelWideYouTubeClaimGuard>(new ChannelWideYouTubeClaimGuard(
            _mocker.GetMock<IPodcastRepository>().Object,
            _mocker.GetMock<INewsOrganisationRepository>().Object,
            _mocker.GetMock<ITvShowRepository>().Object,
            NullLogger<ChannelWideYouTubeClaimGuard>.Instance));
        _mocker.Use<IServiceIdsUniqueness>(new ServiceIdsUniqueness(
            _mocker.GetMock<INewsOrganisationRepository>().Object,
            _mocker.GetMock<ITvShowRepository>().Object,
            _mocker.GetMock<IPodcastRepository>().Object,
            NullLogger<ServiceIdsUniqueness>.Instance));
    }

    private NewsOrganisation AddOrganisation(CatalogueIndexingSource? indexing = null, bool removed = false)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Indexing = indexing,
            Removed = removed ? true : null
        };
        _organisations.Add(organisation);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        return organisation;
    }

    private TvShow AddShow(CatalogueIndexingSource? indexing = null, bool removed = false)
    {
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Indexing = indexing,
            Removed = removed ? true : null
        };
        _shows.Add(show);
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        return show;
    }

    private Podcast AddPodcast(Action<Podcast> customise)
    {
        var podcast = _fixture.CreatePodcast(customise);
        _podcasts.Add(podcast);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync((Expression<Func<Podcast, bool>> match) =>
                _podcasts.FirstOrDefault(match.Compile()));
        return podcast;
    }

    private Task<PodcastUpdateResult> PatchPodcast(Podcast podcast, PodcastChangeRequest change) =>
        _mocker.CreateInstance<PodcastUpdateService>()
            .UpdateAsync(new PodcastChangeRequestWrapper(podcast.Id, change), CancellationToken.None);

    private Task<NewsOrganisationUpdateResult> PatchOrganisation(
        NewsOrganisation organisation, NewsOrganisationChangeRequest change) =>
        _mocker.CreateInstance<NewsOrganisationService>()
            .UpdateAsync(new NewsOrganisationChangeRequestWrapper(organisation.Id, change), CancellationToken.None);

    private Task<TvShowUpdateResult> PatchShow(TvShow show, TvShowChangeRequest change) =>
        _mocker.CreateInstance<TvShowService>()
            .UpdateAsync(new TvShowChangeRequestWrapper(show.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH podcast channel-wide on a channel a channel-wide news organisation already claims is a BadRequest with channelWideYouTubeConflict asking for a playlist id, and nothing is saved.")]
    public async Task podcast_refused_when_news_claims_channel_wide()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = channelId });
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = string.Empty;
        });

        // Act
        var result = await PatchPodcast(podcast, new PodcastChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.BadRequest);
        result.Code.Should().Be(ChannelWideYouTubeClaimConflict.Code);
        result.Error.Should().Contain(organisation.Id.ToString());
        result.Error.Should().Contain("youTubePlaylistId");
        podcast.YouTubeChannelId.Should().BeEmpty();
        _savedPodcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation channel-wide on a channel a channel-wide podcast already claims is a BadRequest with channelWideYouTubeConflict asking for a playlist id, and nothing is saved.")]
    public async Task news_refused_when_podcast_claims_channel_wide()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
        });
        var organisation = AddOrganisation();

        // Act
        var result = await PatchOrganisation(organisation,
            new NewsOrganisationChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.BadRequest);
        result.Code.Should().Be(ChannelWideYouTubeClaimConflict.Code);
        result.Message.Should().Contain(podcast.Id.ToString());
        result.Message.Should().Contain("youTubePlaylistId");
        _savedOrganisation.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH TV show channel-wide on a channel a channel-wide podcast already claims is a BadRequest with channelWideYouTubeConflict, and nothing is saved.")]
    public async Task tv_show_refused_when_podcast_claims_channel_wide()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
        });
        var show = AddShow();

        // Act
        var result = await PatchShow(show, new TvShowChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.BadRequest);
        result.Code.Should().Be(ChannelWideYouTubeClaimConflict.Code);
        result.Message.Should().Contain(podcast.Id.ToString());
        _savedShow.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH podcast with a playlist on a channel a channel-wide news organisation claims is accepted, because playlist-scoped parents may share a channel.")]
    public async Task playlist_scoped_podcast_may_share_channel()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = channelId });
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = string.Empty;
        });

        // Act
        var result = await PatchPodcast(podcast, new PodcastChangeRequest
        {
            YouTubeChannelId = channelId,
            YouTubePlaylistId = playlistId
        });

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _savedPodcast.Should().NotBeNull();
        _savedPodcast!.YouTubeChannelId.Should().Be(channelId);
        _savedPodcast.YouTubePlaylistId.Should().Be(playlistId);
    }

    [Fact(DisplayName =
        "PATCH channel-wide podcast on a channel only a playlist-scoped news organisation uses is accepted, because the outlet is not a channel-wide claimant.")]
    public async Task channel_wide_podcast_allowed_when_news_is_playlist_scoped()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = channelId,
            YouTubePlaylistId = _fixture.CreateYouTubePlaylistId()
        });
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = string.Empty;
        });

        // Act
        var result = await PatchPodcast(podcast, new PodcastChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _savedPodcast!.YouTubeChannelId.Should().Be(channelId);
    }

    [Fact(DisplayName =
        "PATCH channel-wide podcast on a channel only a removed channel-wide news organisation claims is accepted, because removed claimants are ignored.")]
    public async Task removed_claimant_is_ignored()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = channelId }, removed: true);
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = string.Empty;
        });

        // Act
        var result = await PatchPodcast(podcast, new PodcastChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _savedPodcast.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "PATCH podcast clearing its playlist while keeping a channel a channel-wide TV show claims is a BadRequest with channelWideYouTubeConflict, because the clear would create a channel-wide clash.")]
    public async Task clearing_playlist_that_creates_clash_is_refused()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var show = AddShow(new CatalogueIndexingSource { YouTubeChannelId = channelId });
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = playlistId;
        });

        // Act
        var result = await PatchPodcast(podcast, new PodcastChangeRequest { YouTubePlaylistId = " " });

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.BadRequest);
        result.Code.Should().Be(ChannelWideYouTubeClaimConflict.Code);
        result.Error.Should().Contain(show.Id.ToString());
        podcast.YouTubePlaylistId.Should().Be(playlistId);
        _savedPodcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation clearing its playlist while keeping a channel a channel-wide podcast claims is a BadRequest with channelWideYouTubeConflict, and nothing is saved.")]
    public async Task news_clearing_playlist_that_creates_clash_is_refused()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
        });
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = channelId,
            YouTubePlaylistId = playlistId
        });

        // Act
        var result = await PatchOrganisation(organisation,
            new NewsOrganisationChangeRequest { YouTubePlaylistId = "" });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.BadRequest);
        result.Code.Should().Be(ChannelWideYouTubeClaimConflict.Code);
        result.Message.Should().Contain(podcast.Id.ToString());
        organisation.Indexing!.YouTubePlaylistId.Should().Be(playlistId);
        _savedOrganisation.Should().BeNull();
    }
}
