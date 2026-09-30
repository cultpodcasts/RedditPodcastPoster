using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Formatting;
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
    private IReadOnlyList<EpisodeSearchRecord>? _uploadedSearchDocuments;
    private bool _searchUploadSucceeds = true;
    private readonly List<Guid> _deletedTvShowIds = [];
    private readonly List<(Guid ParentId, Guid PlayableId)> _deletedTvEpisodeIds = [];

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
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>()))
            .Callback<Guid>(id => _deletedTvShowIds.Add(id))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => _savedTvEpisode = episode)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Callback<Guid, Guid>((parentId, playableId) => _deletedTvEpisodeIds.Add((parentId, playableId)))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((NewsOrganisation?)null);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(organisation => _savedOrganisation = organisation)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(report => _savedReport = report)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>(
                (documents, _) => _uploadedSearchDocuments = documents)
            .ReturnsAsync(() => _searchUploadSucceeds);
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
        "When a TV show already exists at the podcast id and the source podcast is still present, transfer continues so a retry is not Conflict.")]
    public async Task existing_tv_show_with_source_present_continues()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
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
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Transfer returns Conflict when dest parent exists at the kept id and the source podcast is already gone.")]
    public async Task dest_exists_source_gone_is_conflict()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var showName = _fixture.CreateTitle();
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(podcastId))
            .ReturnsAsync(new TvShow(showName) { Id = podcastId });
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcastId,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Conflict);
        result.ParentId.Should().Be(podcastId);
        _savedShow.Should().BeNull();
    }

    [Fact(DisplayName =
        "Transfer returns Conflict when a news organisation already exists at the same guid as a TV-show transfer, because one id is TvShow or NewsOrganisation not both.")]
    public async Task sibling_news_organisation_at_same_guid_is_conflict()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(podcast.Id))
            .ReturnsAsync(new NewsOrganisation(podcast.Name) { Id = podcast.Id });
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Conflict);
        _savedShow.Should().BeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When dest playable save fails after the parent write, dest rows written in this attempt are compensated and the source podcast remains.")]
    public async Task dest_write_failure_compensates_and_keeps_source()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var firstEpisode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var secondEpisode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(firstEpisode, secondEpisode);
        var playableSaves = 0;
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Returns<TvShowEpisode>(playable =>
            {
                playableSaves++;
                if (playableSaves > 1)
                {
                    return Task.FromException(new InvalidOperationException(_fixture.Create<string>()));
                }

                _savedTvEpisode = playable;
                return Task.CompletedTask;
            });
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Failed);
        _deletedTvShowIds.Should().Contain(podcast.Id);
        _deletedTvEpisodeIds.Should().ContainSingle();
        _deletedTvEpisodeIds[0].ParentId.Should().Be(podcast.Id);
        new[] { firstEpisode.Id, secondEpisode.Id }.Should().Contain(_deletedTvEpisodeIds[0].PlayableId);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        (await _episodes.GetEpisode(podcast.Id, firstEpisode.Id)).Should().NotBeNull();
        (await _episodes.GetEpisode(podcast.Id, secondEpisode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Search swap uploads the same episode guid with ContentKind TvShowEpisode, description capped at 100, and no seriesDescription.")]
    public async Task search_swap_uploads_tv_show_episode_kind_same_guid_no_series_description()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Description = new string('a', Constants.DescriptionSize - 10) + " extra words beyond the search cap";
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
        result.FailureIndexingPlayables.Should().BeFalse();
        _uploadedSearchDocuments.Should().ContainSingle();
        var document = _uploadedSearchDocuments![0];
        document.Id.Should().Be(episode.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.TvShowEpisode);
        document.Description.Should().Be(DescriptionTruncator.TruncateForSearch(episode.Description));
        document.Description!.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        typeof(EpisodeSearchRecord).GetProperty("SeriesDescription").Should().BeNull();
        JsonSerializer.Serialize(document).Should().NotContain("seriesDescription");
    }

    [Fact(DisplayName =
        "Search swap for a news transfer uploads ContentKind NewsReport on the same episode guid.")]
    public async Task search_swap_uploads_news_report_kind()
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
        _uploadedSearchDocuments.Should().ContainSingle();
        var document = _uploadedSearchDocuments![0];
        document.Id.Should().Be(episode.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.NewsReport);
        JsonSerializer.Serialize(document).Should().NotContain("seriesDescription");
    }

    [Fact(DisplayName =
        "When search swap UploadAsync returns false, transfer is Accepted with FailureIndexingPlayables because dest writes already succeeded.")]
    public async Task search_swap_failure_sets_failure_indexing_playables()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        _searchUploadSucceeds = false;
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        result.FailureIndexingPlayables.Should().BeTrue();
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
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
