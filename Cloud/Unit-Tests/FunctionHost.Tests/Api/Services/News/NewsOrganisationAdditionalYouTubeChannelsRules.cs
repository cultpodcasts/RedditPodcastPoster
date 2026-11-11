using System.Linq.Expressions;
using Api.Dtos.Extensions;
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

namespace FunctionHost.Tests.Api.Services.News;

/// <summary>
/// #1370: a news organisation can hold more than one YouTube channel (<c>additionalYouTubeChannels</c> beside
/// <c>youTubeChannelId</c>, each with an optional playlist on that channel; the older <c>additionalYouTubeChannelIds</c>
/// body is still accepted as whole channels). The curator PATCH stores, replaces and clears the list; each new playlist
/// is validated against its channel, and each additional channel passes the same uniqueness and channel-wide claim
/// checks as the main channel, in both directions, before anything is saved.
/// </summary>
public class NewsOrganisationAdditionalYouTubeChannelsRules
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

    public NewsOrganisationAdditionalYouTubeChannelsRules()
    {
        _mocker.Use(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SearchClient)) as SearchClient);
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(NullLogger<PodcastUpdateService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.Check(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((YouTubePlaylistValidation?)null);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelAgainstPlaylist(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((YouTubePlaylistValidation?)null);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelOwnsPlaylist(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetByPodcastId(It.IsAny<Guid>()))
            .Returns(AsyncEnumerable.Empty<RedditPodcastPoster.Models.Episodes.Episode>());

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
        "PATCH news organisation additionalYouTubeChannelIds stores the channels trimmed and without repeats beside the main channel, and the GET returns them, because an outlet with a main and an archive channel is one organisation.")]
    public async Task additional_channels_are_stored_and_returned()
    {
        // Arrange
        var mainChannelId = _fixture.CreateYouTubeChannelId();
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var clipsChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = mainChannelId });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannelIds = [$" {archiveChannelId} ", clipsChannelId, archiveChannelId, " "]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _savedOrganisation.Should().BeSameAs(organisation);
        organisation.Indexing!.YouTubeChannelId.Should().Be(mainChannelId);
        organisation.Indexing.AdditionalYouTubeChannelIdList().Should().Equal(archiveChannelId, clipsChannelId);
        organisation.ToDto().AdditionalYouTubeChannelIds.Should().Equal(archiveChannelId, clipsChannelId);
    }

    [Fact(DisplayName =
        "PATCH news organisation additionalYouTubeChannelIds [] clears the stored channels and leaves the main channel, because a list replaces the stored list.")]
    public async Task empty_list_clears_additional_channels()
    {
        // Arrange
        var mainChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = mainChannelId,
            AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(_fixture.CreateYouTubeChannelId())]
        });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannelIds = []
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing!.AdditionalYouTubeChannels.Should().BeNull();
        organisation.Indexing.YouTubeChannelId.Should().Be(mainChannelId);
        organisation.ToDto().AdditionalYouTubeChannelIds.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "PATCH news organisation without additionalYouTubeChannelIds keeps the stored channels, because an omitted field is unchanged.")]
    public async Task omitted_list_keeps_additional_channels()
    {
        // Arrange
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(archiveChannelId)]
        });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            Description = _fixture.Create<string>()
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing!.AdditionalYouTubeChannelIdList().Should().Equal(archiveChannelId);
    }

    [Fact(DisplayName =
        "PATCH news organisation with an additional channel that is not a YouTube channel id is a BadRequest and nothing is saved, because a pasted handle or URL would silently index nothing.")]
    public async Task malformed_additional_channel_is_refused()
    {
        // Arrange
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannelIds = ["@" + _fixture.Create<string>()]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.BadRequest);
        result.Code.Should().Be(AdditionalYouTubeChannels.InvalidChannelIdCode);
        _savedOrganisation.Should().BeNull();
        organisation.Indexing!.AdditionalYouTubeChannels.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation with its own main channel as an additional channel is a BadRequest and nothing is saved, because a channel is either the main channel or an additional one.")]
    public async Task main_channel_cannot_be_additional()
    {
        // Arrange
        var mainChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = mainChannelId });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannelIds = [mainChannelId]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.BadRequest);
        result.Code.Should().Be(AdditionalYouTubeChannels.MainChannelCode);
        _savedOrganisation.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation main youTubeChannelId set to one of its additional channels is a BadRequest and nothing is saved, because a channel is either the main channel or an additional one.")]
    public async Task main_channel_cannot_move_onto_an_additional_channel()
    {
        // Arrange
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(archiveChannelId)]
        });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            YouTubeChannelId = archiveChannelId
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.BadRequest);
        result.Code.Should().Be(AdditionalYouTubeChannels.MainChannelCode);
        _savedOrganisation.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation additional channel that another news organisation holds as its main channel is a 409 naming that organisation and saves nothing, because submission could not tell the two apart.")]
    public async Task additional_channel_held_by_another_organisation_conflicts()
    {
        // Arrange
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var holder = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = archiveChannelId });
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannelIds = [archiveChannelId]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Conflict);
        result.ConflictingIds.Should().Equal(holder.Id);
        result.Message.Should().Contain("additionalYouTubeChannelIds");
        _savedOrganisation.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH TV show channel-wide on a channel a news organisation holds as a whole additional channel is a BadRequest with channelWideYouTubeConflict naming the organisation, because that additional channel is claimed channel-wide.")]
    public async Task tv_show_refused_on_an_additional_channel()
    {
        // Arrange
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(archiveChannelId)]
        });
        var show = AddShow();

        // Act
        var result = await PatchShow(show, new TvShowChangeRequest { YouTubeChannelId = archiveChannelId });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.BadRequest);
        result.Code.Should().Be(ChannelWideYouTubeClaimConflict.Code);
        result.Message.Should().Contain(organisation.Id.ToString());
        _savedShow.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation whole additional channel that a channel-wide podcast claims is a BadRequest with channelWideYouTubeConflict and nothing is saved, because a whole additional channel is claimed channel-wide.")]
    public async Task additional_channel_refused_when_podcast_claims_channel_wide()
    {
        // Arrange
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = archiveChannelId;
            p.YouTubePlaylistId = string.Empty;
        });
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannelIds = [archiveChannelId]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.BadRequest);
        result.Code.Should().Be(ChannelWideYouTubeClaimConflict.Code);
        result.Message.Should().Contain(podcast.Id.ToString());
        _savedOrganisation.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH podcast channel-wide on a channel a news organisation holds as an additional channel is a BadRequest with channelWideYouTubeConflict, even when the organisation's main channel is playlist-scoped, because the additional channel is claimed whole.")]
    public async Task podcast_refused_on_an_additional_channel()
    {
        // Arrange
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            YouTubePlaylistId = _fixture.CreateYouTubePlaylistId(),
            AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(archiveChannelId)]
        });
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = string.Empty;
        });

        // Act
        var result = await PatchPodcast(podcast, new PodcastChangeRequest { YouTubeChannelId = archiveChannelId });

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.BadRequest);
        result.Code.Should().Be(ChannelWideYouTubeClaimConflict.Code);
        result.Error.Should().Contain(organisation.Id.ToString());
        _savedPodcast.Should().BeNull();
    }

    private void PlaylistOwnership(string channelId, string playlistId, YouTubePlaylistValidation? result) =>
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelOwnsPlaylist(channelId, playlistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void VerifyOwnershipChecks(Times times) =>
        _mocker.GetMock<IYouTubePlaylistGuard>().Verify(
            g => g.CheckChannelOwnsPlaylist(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            times);

    [Fact(DisplayName =
        "PATCH news organisation additionalYouTubeChannels stores each channel with its optional playlist once the playlist is confirmed to belong to that channel, and the GET returns the channels with their playlists (and the ids alone for older clients), because a news organisation may index one whole channel and only one playlist of another.")]
    public async Task additional_channels_with_playlists_are_stored_and_returned()
    {
        // Arrange
        var mainChannelId = _fixture.CreateYouTubeChannelId();
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var clipsChannelId = _fixture.CreateYouTubeChannelId();
        var clipsPlaylistId = _fixture.CreateYouTubePlaylistId();
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = mainChannelId });
        PlaylistOwnership(clipsChannelId, clipsPlaylistId, null);

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannels =
            [
                new AdditionalYouTubeChannelChange($" {archiveChannelId} "),
                new AdditionalYouTubeChannelChange(clipsChannelId, $" {clipsPlaylistId} "),
                new AdditionalYouTubeChannelChange(archiveChannelId, " ")
            ]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _savedOrganisation.Should().BeSameAs(organisation);
        organisation.Indexing!.AdditionalYouTubeChannelIdList().Should().Equal(archiveChannelId, clipsChannelId);
        organisation.Indexing.YouTubePlaylistIdOn(archiveChannelId).Should().BeNull();
        organisation.Indexing.YouTubePlaylistIdOn(clipsChannelId).Should().Be(clipsPlaylistId);
        var dto = organisation.ToDto();
        dto.AdditionalYouTubeChannels.Select(x => (x.YouTubeChannelId, x.YouTubePlaylistId)).Should()
            .Equal((archiveChannelId, null), (clipsChannelId, clipsPlaylistId));
        dto.AdditionalYouTubeChannelIds.Should().Equal(archiveChannelId, clipsChannelId);
        _mocker.GetMock<IYouTubePlaylistGuard>().Verify(
            g => g.CheckChannelOwnsPlaylist(clipsChannelId, clipsPlaylistId, It.IsAny<CancellationToken>()), Times.Once);
        VerifyOwnershipChecks(Times.Once());
    }

    [Theory(DisplayName =
        "PATCH news organisation with a playlist on an additional channel that is malformed, missing or private, or owned by another channel is a BadRequest with the main-channel playlist code, and a playlist lookup that fails is a 503 youTubePlaylistLookupFailed; nothing is saved, because each channel's playlist must belong to that channel.")]
    [InlineData(YouTubePlaylistValidationStatus.InvalidPlaylistId, "youTubePlaylistIdInvalid", NewsOrganisationUpdateStatus.BadRequest)]
    [InlineData(YouTubePlaylistValidationStatus.PlaylistNotFound, "youTubePlaylistNotFound", NewsOrganisationUpdateStatus.BadRequest)]
    [InlineData(YouTubePlaylistValidationStatus.ChannelMismatch, "youTubePlaylistChannelMismatch", NewsOrganisationUpdateStatus.BadRequest)]
    [InlineData(YouTubePlaylistValidationStatus.LookupFailed, "youTubePlaylistLookupFailed", NewsOrganisationUpdateStatus.ServiceUnavailable)]
    public async Task refused_additional_channel_playlist_is_not_saved(
        YouTubePlaylistValidationStatus status,
        string code,
        NewsOrganisationUpdateStatus expected)
    {
        // Arrange
        var clipsChannelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });
        PlaylistOwnership(clipsChannelId, playlistId,
            new YouTubePlaylistValidation(status, playlistId, _fixture.CreateYouTubeChannelId()));

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannels = [new AdditionalYouTubeChannelChange(clipsChannelId, playlistId)]
        });

        // Assert
        result.Status.Should().Be(expected);
        result.Code.Should().Be(code);
        result.Message.Should().Contain(clipsChannelId);
        _savedOrganisation.Should().BeNull();
        organisation.Indexing!.AdditionalYouTubeChannels.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation re-sending an additional channel with its stored playlist makes no YouTube call and keeps that channel's indexer flags, while changing a channel's playlist validates the new playlist and drops only that channel's expensive-query flag, because the flags describe the playlist the indexer measured.")]
    public async Task stored_playlists_and_indexer_flags_are_kept_until_the_playlist_changes()
    {
        // Arrange
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var clipsChannelId = _fixture.CreateYouTubeChannelId();
        var archivePlaylistId = _fixture.CreateYouTubePlaylistId();
        var oldClipsPlaylistId = _fixture.CreateYouTubePlaylistId();
        var newClipsPlaylistId = _fixture.CreateYouTubePlaylistId();
        var archive = AdditionalYouTubeChannel.ForPlaylist(archiveChannelId, archivePlaylistId);
        archive.YouTubePlaylistQueryIsExpensive = true;
        archive.YouTubeChannelSearchForbidden = true;
        var clips = AdditionalYouTubeChannel.ForPlaylist(clipsChannelId, oldClipsPlaylistId);
        clips.YouTubePlaylistQueryIsExpensive = true;
        clips.YouTubeChannelSearchForbidden = true;
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannels = [archive, clips]
        });
        PlaylistOwnership(clipsChannelId, newClipsPlaylistId, null);

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannels =
            [
                new AdditionalYouTubeChannelChange(archiveChannelId, archivePlaylistId),
                new AdditionalYouTubeChannelChange(clipsChannelId, newClipsPlaylistId)
            ]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        var storedArchive = organisation.Indexing!.AdditionalYouTubeChannelFor(archiveChannelId)!;
        storedArchive.YouTubePlaylistId.Should().Be(archivePlaylistId);
        storedArchive.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
        storedArchive.YouTubeChannelSearchForbidden.Should().BeTrue();
        var storedClips = organisation.Indexing.AdditionalYouTubeChannelFor(clipsChannelId)!;
        storedClips.YouTubePlaylistId.Should().Be(newClipsPlaylistId);
        storedClips.YouTubePlaylistQueryIsExpensive.Should().BeNull();
        storedClips.YouTubeChannelSearchForbidden.Should().BeTrue();
        _mocker.GetMock<IYouTubePlaylistGuard>().Verify(
            g => g.CheckChannelOwnsPlaylist(archiveChannelId, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyOwnershipChecks(Times.Once());
    }

    [Fact(DisplayName =
        "PATCH news organisation additionalYouTubeChannelIds (the older ids-only body) stores every channel whole, clearing a stored playlist on a re-sent channel, because that body cannot carry a playlist.")]
    public async Task legacy_ids_body_stores_whole_channels()
    {
        // Arrange
        var clipsChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannels =
                [AdditionalYouTubeChannel.ForPlaylist(clipsChannelId, _fixture.CreateYouTubePlaylistId())]
        });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannelIds = [clipsChannelId]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing!.AdditionalYouTubeChannelFor(clipsChannelId)!.YouTubePlaylistId.Should().BeNull();
        organisation.Indexing.ClaimsYouTubeChannelWide(clipsChannelId).Should().BeTrue();
        VerifyOwnershipChecks(Times.Never());
    }

    public static TheoryData<string, AdditionalYouTubeChannelChange[]?, string[]?> MalformedRequests()
    {
        var fixture = new DomainTestFixture();
        var channel = fixture.CreateYouTubeChannelId();
        return new TheoryData<string, AdditionalYouTubeChannelChange[]?, string[]?>
        {
            { "both body fields", [new AdditionalYouTubeChannelChange(channel)], [channel] },
            { "a playlist without a channel", [new AdditionalYouTubeChannelChange(" ", fixture.CreateYouTubePlaylistId())], null },
            { "a malformed channel id", [new AdditionalYouTubeChannelChange("https://youtube.com/@x", fixture.CreateYouTubePlaylistId())], null },
            {
                "one channel with two playlists",
                [
                    new AdditionalYouTubeChannelChange(channel, fixture.CreateYouTubePlaylistId()),
                    new AdditionalYouTubeChannelChange(channel, fixture.CreateYouTubePlaylistId())
                ],
                null
            }
        };
    }

    [Theory(DisplayName =
        "PATCH news organisation with both additionalYouTubeChannels and additionalYouTubeChannelIds, a playlist without a channel id, a malformed channel id, or one channel listed with two different playlists is a BadRequest invalidAdditionalYouTubeChannelId with no YouTube call and nothing saved, because the stored channels would be ambiguous.")]
    [MemberData(nameof(MalformedRequests))]
    public async Task malformed_additional_channel_requests_are_refused(
        string refusedCase,
        AdditionalYouTubeChannelChange[]? channels,
        string[]? ids)
    {
        // Arrange
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannels = channels,
            AdditionalYouTubeChannelIds = ids
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.BadRequest);
        result.Code.Should().Be(AdditionalYouTubeChannels.InvalidChannelIdCode, refusedCase);
        _savedOrganisation.Should().BeNull();
        VerifyOwnershipChecks(Times.Never());
    }

    [Fact(DisplayName =
        "PATCH news organisation with its main channel as a playlist-scoped additional channel is a BadRequest additionalYouTubeChannelIsMainChannel with no YouTube call, because a channel is either the main channel or an additional one whatever its playlist.")]
    public async Task main_channel_cannot_be_a_playlist_scoped_additional_channel()
    {
        // Arrange
        var mainChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = mainChannelId });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannels =
                [new AdditionalYouTubeChannelChange(mainChannelId, _fixture.CreateYouTubePlaylistId())]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.BadRequest);
        result.Code.Should().Be(AdditionalYouTubeChannels.MainChannelCode);
        _savedOrganisation.Should().BeNull();
        VerifyOwnershipChecks(Times.Never());
    }

    [Fact(DisplayName =
        "PATCH news organisation playlist-scoped additional channel that another TV show holds is a 409 naming the show, because a channel is held by one news organisation or TV show whatever playlist scopes it.")]
    public async Task playlist_scoped_additional_channel_held_by_a_show_conflicts()
    {
        // Arrange
        var clipsChannelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var show = AddShow(new CatalogueIndexingSource
        {
            YouTubeChannelId = clipsChannelId,
            YouTubePlaylistId = _fixture.CreateYouTubePlaylistId()
        });
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });
        PlaylistOwnership(clipsChannelId, playlistId, null);

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannels = [new AdditionalYouTubeChannelChange(clipsChannelId, playlistId)]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Conflict);
        result.ConflictingIds.Should().Equal(show.Id);
        result.Message.Should().Contain("additionalYouTubeChannels");
        _savedOrganisation.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation playlist-scoped additional channel that a playlist-scoped podcast shares is accepted, because submission routes those videos by playlist membership.")]
    public async Task playlist_scoped_additional_channel_may_share_with_playlist_scoped_podcast()
    {
        // Arrange
        var clipsChannelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        AddPodcast(p =>
        {
            p.YouTubeChannelId = clipsChannelId;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });
        PlaylistOwnership(clipsChannelId, playlistId, null);

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannels = [new AdditionalYouTubeChannelChange(clipsChannelId, playlistId)]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing!.YouTubePlaylistIdOn(clipsChannelId).Should().Be(playlistId);
    }

    [Fact(DisplayName =
        "PATCH podcast channel-wide on a channel a news organisation holds only through a playlist is not refused as a channel-wide claim conflict, because a playlist-scoped additional channel makes no channel-wide claim.")]
    public async Task podcast_not_claim_refused_on_playlist_scoped_additional_channel()
    {
        // Arrange
        var clipsChannelId = _fixture.CreateYouTubeChannelId();
        AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannels =
                [AdditionalYouTubeChannel.ForPlaylist(clipsChannelId, _fixture.CreateYouTubePlaylistId())]
        });
        var podcast = AddPodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = string.Empty;
        });

        // Act
        var result = await PatchPodcast(podcast, new PodcastChangeRequest { YouTubeChannelId = clipsChannelId });

        // Assert
        result.Code.Should().NotBe(ChannelWideYouTubeClaimConflict.Code);
    }
}
