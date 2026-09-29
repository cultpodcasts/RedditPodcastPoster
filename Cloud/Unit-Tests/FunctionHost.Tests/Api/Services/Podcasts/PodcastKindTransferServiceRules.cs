using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastKindTransferServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private TvShow? _savedShow;
    private TvShowEpisode? _savedTvEpisode;
    private NewsOrganisation? _savedOrganisation;
    private NewsReport? _savedReport;

    public PodcastKindTransferServiceRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((TvShow?)null);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show => _savedShow = show)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => _savedTvEpisode = episode)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((NewsOrganisation?)null);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(organisation => _savedOrganisation = organisation)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(report => _savedReport = report)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPodcastKindSearchSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mocker.GetMock<ILogger<PodcastKindTransferService>>();
    }

    [Fact(DisplayName =
        "Transferring a podcast to a TV show saves the TV show and episode under the original guids, then deletes the podcast rows.")]
    public async Task transfer_to_tv_show_keeps_guids_and_deletes_podcast()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        result.ParentId.Should().Be(podcast.Id);
        result.TargetKind.Should().Be(CatalogueParentKind.TvShow);
        _savedShow.Should().NotBeNull();
        _savedShow!.Id.Should().Be(podcast.Id);
        _savedTvEpisode.Should().NotBeNull();
        _savedTvEpisode!.Id.Should().Be(episode.Id);
        _savedTvEpisode.TvShowId.Should().Be(podcast.Id);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Transferring a podcast to a news organisation saves the organisation and report under the original guids.")]
    public async Task transfer_to_news_organisation_keeps_guids()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        result.ParentId.Should().Be(podcast.Id);
        _savedOrganisation.Should().NotBeNull();
        _savedOrganisation!.Id.Should().Be(podcast.Id);
        _savedReport.Should().NotBeNull();
        _savedReport!.Id.Should().Be(episode.Id);
        _savedReport.NewsOrganisationId.Should().Be(podcast.Id);
    }

    [Fact(DisplayName =
        "A missing target kind is rejected without writing catalogue rows, because Film is not a parent transfer.")]
    public async Task missing_target_is_invalid()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = null },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.InvalidTarget);
        _savedShow.Should().BeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Transfer returns Conflict when a TV show already exists at the podcast id, so guids are not reused.")]
    public async Task existing_tv_show_id_is_conflict()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(podcast.Id))
            .ReturnsAsync(new TvShow(podcast.Name) { Id = podcast.Id });
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Conflict);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Transfer returns NotFound when the podcast id is missing.")]
    public async Task missing_podcast_is_not_found()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            _fixture.CreateGuid(),
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.NotFound);
    }
}
