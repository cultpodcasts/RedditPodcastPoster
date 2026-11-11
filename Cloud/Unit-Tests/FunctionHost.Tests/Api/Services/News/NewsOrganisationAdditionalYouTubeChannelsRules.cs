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
/// #1370: a news organisation can hold more than one YouTube channel (<c>additionalYouTubeChannelIds</c> beside
/// <c>youTubeChannelId</c>). The curator PATCH stores, replaces and clears the list; each additional channel passes the
/// same uniqueness and channel-wide claim checks as the main channel, in both directions, before anything is saved.
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
        organisation.Indexing.AdditionalYouTubeChannelIds.Should().Equal(archiveChannelId, clipsChannelId);
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
            AdditionalYouTubeChannelIds = [_fixture.CreateYouTubeChannelId()]
        });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            AdditionalYouTubeChannelIds = []
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing!.AdditionalYouTubeChannelIds.Should().BeNull();
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
            AdditionalYouTubeChannelIds = [archiveChannelId]
        });

        // Act
        var result = await PatchOrganisation(organisation, new NewsOrganisationChangeRequest
        {
            Description = _fixture.Create<string>()
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        organisation.Indexing!.AdditionalYouTubeChannelIds.Should().Equal(archiveChannelId);
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
        organisation.Indexing!.AdditionalYouTubeChannelIds.Should().BeNull();
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
            AdditionalYouTubeChannelIds = [archiveChannelId]
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
        "PATCH TV show channel-wide on a channel a news organisation holds as an additional channel is a BadRequest with channelWideYouTubeConflict naming the organisation, because an additional channel is always claimed channel-wide.")]
    public async Task tv_show_refused_on_an_additional_channel()
    {
        // Arrange
        var archiveChannelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannelIds = [archiveChannelId]
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
        "PATCH news organisation additional channel that a channel-wide podcast claims is a BadRequest with channelWideYouTubeConflict and nothing is saved, because an additional channel is always claimed channel-wide.")]
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
            AdditionalYouTubeChannelIds = [archiveChannelId]
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
}
