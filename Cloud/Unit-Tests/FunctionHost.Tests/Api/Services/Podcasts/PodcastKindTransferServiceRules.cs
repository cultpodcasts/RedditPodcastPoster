using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions;
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
    private readonly List<TvShowEpisode> _savedTvEpisodes = [];
    private NewsOrganisation? _savedOrganisation;
    private NewsReport? _savedReport;
    private readonly List<NewsReport> _savedReports = [];
    private IReadOnlyList<EpisodeSearchRecord>? _uploadedSearchDocuments;
    private IReadOnlyList<Guid>? _deletedSearchIds;
    private bool _searchUploadSucceeds = true;
    private bool _searchDeleteSucceeds = true;
    private readonly List<Guid> _deletedTvShowIds = [];
    private readonly List<(Guid ParentId, Guid PlayableId)> _deletedTvEpisodeIds = [];
    private readonly InMemoryParentKindTransferLockStore _locks = new();
    private DateTime _now;
    private TimeSpan _clockStep = TimeSpan.Zero;

    public PodcastKindTransferServiceRules()
    {
        _now = _fixture.Create<DateTime>().ToUniversalTime();
        _mocker.Use<IParentKindTransferLockStore>(_locks);
        _mocker.GetMock<TimeProvider>()
            .Setup(t => t.GetUtcNow())
            .Returns(() =>
            {
                lock (_locks)
                {
                    _now += _clockStep;
                    return new DateTimeOffset(_now, TimeSpan.Zero);
                }
            });
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
            .Callback<TvShowEpisode>(episode =>
            {
                lock (_savedTvEpisodes)
                {
                    _savedTvEpisode = episode;
                    _savedTvEpisodes.Add(episode);
                }
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Callback<Guid, Guid>((parentId, playableId) =>
            {
                lock (_deletedTvEpisodeIds)
                {
                    _deletedTvEpisodeIds.Add((parentId, playableId));
                }
            })
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
            .Callback<NewsReport>(report =>
            {
                lock (_savedReports)
                {
                    _savedReport = report;
                    _savedReports.Add(report);
                }
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>(
                (documents, _) => _uploadedSearchDocuments = documents)
            .ReturnsAsync(() => _searchUploadSucceeds);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.DeleteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => _deletedSearchIds = ids)
            .ReturnsAsync(() => _searchDeleteSucceeds);
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
        "Transferring a podcast fills missing Services urls from nested ids (deterministic, no platform clients) before mapping, so each TV episode keeps every platform in Services only without sequential Spotify/Apple/YouTube lookups.")]
    public async Task transfer_fills_service_urls_from_ids_for_every_episode()
    {
        // Arrange
        var appleShowId = _fixture.CreateAppleId();
        var podcast = _fixture.CreatePodcast(p => p.AppleId = appleShowId);
        var firstSpotify = _fixture.CreateSpotifyId();
        var firstYouTube = _fixture.CreateYouTubeId();
        var firstApple = _fixture.CreateAppleId();
        var secondSpotify = _fixture.CreateSpotifyId();
        var secondYouTube = _fixture.CreateYouTubeId();
        var first = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        first.Services = null;
        first.Ids = new EpisodeIds { Spotify = firstSpotify, Apple = firstApple, YouTube = firstYouTube };
        var second = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        second.Services = null;
        second.Ids = new EpisodeIds { Spotify = secondSpotify, YouTube = secondYouTube };
        _podcasts.Seed(podcast);
        _episodes.Seed(first, second);
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        _savedTvEpisodes.Should().HaveCount(2);
        var savedFirst = _savedTvEpisodes.Single(item => item.Id == first.Id);
        savedFirst.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(firstSpotify));
        savedFirst.Services[ServiceKeys.YouTube].Url.Should().Be(_fixture.DefaultYouTubeUrl(firstYouTube));
        savedFirst.Services[ServiceKeys.Apple].Url.Should()
            .Be(new Uri($"https://podcasts.apple.com/podcast/id{appleShowId}?i={firstApple}"));
        var savedSecond = _savedTvEpisodes.Single(item => item.Id == second.Id);
        savedSecond.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(secondSpotify));
        savedSecond.Services[ServiceKeys.YouTube].Url.Should().Be(_fixture.DefaultYouTubeUrl(secondYouTube));
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
        "Transferring an auto-indexed podcast to a news organisation keeps its discovery settings (YouTube channel " +
        "id, Spotify and Apple ids, index-all) on the organisation's indexing, because change-kind must not stop " +
        "submits and the hourly indexer from matching the channel.")]
    public async Task transfer_to_news_organisation_keeps_indexing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.AppleId = _fixture.CreateAppleId();
            p.IndexAllEpisodes = true;
        });
        _podcasts.Seed(podcast);
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation },
            CancellationToken.None);

        // Assert
        var indexing = _savedOrganisation!.Indexing;
        indexing.Should().NotBeNull();
        indexing!.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
        indexing.SpotifyId.Should().Be(podcast.SpotifyId);
        indexing.AppleId.Should().Be(podcast.AppleId);
        indexing.IndexAllEpisodes.Should().BeTrue();
    }

    [Theory(DisplayName =
        "Move to news: transferring a podcast to a news organisation saves bypass-short-episodes and " +
        "always-promote-as-hero on even when the podcast had them off or unset, because those are the news defaults.")]
    [InlineData(null)]
    [InlineData(false)]
    public async Task transfer_to_news_organisation_saves_news_default_flags(bool? podcastFlag)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.BypassShortEpisodeChecking = podcastFlag;
            p.AlwaysPromoteAsHero = podcastFlag;
        });
        _podcasts.Seed(podcast);
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation },
            CancellationToken.None);

        // Assert
        _savedOrganisation!.Indexing!.BypassShortEpisodeChecking.Should().BeTrue();
        _savedOrganisation.Indexing.AlwaysPromoteAsHero.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Transferring a podcast to a TV show keeps the podcast's own bypass-short and hero flags (off stays off), " +
        "because the saved news defaults do not apply to TV shows.")]
    public async Task transfer_to_tv_show_keeps_podcast_flags()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.BypassShortEpisodeChecking = false;
            p.AlwaysPromoteAsHero = false;
        });
        _podcasts.Seed(podcast);
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        _savedShow!.Indexing!.BypassShortEpisodeChecking.Should().BeFalse();
        _savedShow.Indexing.AlwaysPromoteAsHero.Should().BeFalse();
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
                if (Interlocked.Increment(ref playableSaves) > 1)
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
        "Search swap uploads the same episode guid with ContentKind TvShowEpisode, description capped at Constants.DescriptionSize, and no seriesDescription.")]
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
        "Transferring a podcast to a news organisation keeps a removed episode removed in Cosmos and does not upload it, " +
        "because a removed report must stay out of the search index.")]
    public async Task transfer_to_news_omits_removed_episode_from_search()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var kept = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var removed = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        removed.Removed = true;
        _podcasts.Seed(podcast);
        _episodes.Seed(kept, removed);
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        result.FailureIndexingPlayables.Should().BeFalse();
        _savedReports.Should().HaveCount(2);
        _savedReports.Should().Contain(report => report.Id == removed.Id && report.Removed);
        _savedReports.Should().Contain(report => report.Id == kept.Id && !report.Removed);
        _uploadedSearchDocuments.Should().ContainSingle().Which.Id.Should().Be(kept.Id.ToString());
        _deletedSearchIds.Should().ContainSingle().Which.Should().Be(removed.Id);
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
        "When deleting a removed episode's search key fails, the transfer is Accepted with FailureIndexingPlayables, " +
        "the removed report stays saved with Removed, and the source episode remains, because a later transfer must be able to retry that delete.")]
    public async Task failed_removed_key_delete_keeps_source_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var kept = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var removed = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        removed.Removed = true;
        _podcasts.Seed(podcast);
        _episodes.Seed(kept, removed);
        _searchDeleteSucceeds = false;
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        result.FailureIndexingPlayables.Should().BeTrue();
        _savedReports.Should().Contain(report => report.Id == removed.Id && report.Removed);
        _uploadedSearchDocuments.Should().ContainSingle().Which.Id.Should().Be(kept.Id.ToString());
        _deletedSearchIds.Should().ContainSingle().Which.Should().Be(removed.Id);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        (await _episodes.GetEpisode(podcast.Id, removed.Id)).Should().NotBeNull();
        (await _episodes.GetEpisode(podcast.Id, kept.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When every episode is removed and deleting those search keys fails, the transfer is Accepted with FailureIndexingPlayables " +
        "and the source podcast remains, because an empty kept-document upload cannot heal a removed key the delete did not drop.")]
    public async Task failed_delete_of_all_removed_publisher_keeps_source()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var removed = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        removed.Removed = true;
        _podcasts.Seed(podcast);
        _episodes.Seed(removed);
        _searchDeleteSucceeds = false;
        var sut = _mocker.CreateInstance<PodcastKindTransferService>();

        // Act
        var result = await sut.TransferAsync(
            podcast.Id,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        result.FailureIndexingPlayables.Should().BeTrue();
        _savedReports.Should().ContainSingle().Which.Removed.Should().BeTrue();
        _uploadedSearchDocuments.Should().BeEmpty();
        _deletedSearchIds.Should().ContainSingle().Which.Should().Be(removed.Id);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        (await _episodes.GetEpisode(podcast.Id, removed.Id)).Should().NotBeNull();
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

    private Task<PodcastKindTransferResult> Transfer(
        Guid podcastId,
        CatalogueParentKind kind,
        CancellationToken cancellationToken = default) =>
        _mocker.CreateInstance<PodcastKindTransferService>().TransferAsync(
            podcastId,
            new PodcastKindTransferRequest { TargetKind = kind },
            cancellationToken);

    private void ArrangeExistingDestParent(CatalogueParentKind kind, Podcast podcast)
    {
        if (kind == CatalogueParentKind.TvShow)
        {
            _mocker.GetMock<ITvShowRepository>()
                .Setup(r => r.GetTvShow(podcast.Id))
                .ReturnsAsync(new TvShow(podcast.Name) { Id = podcast.Id });
        }
        else
        {
            _mocker.GetMock<INewsOrganisationRepository>()
                .Setup(r => r.GetNewsOrganisation(podcast.Id))
                .ReturnsAsync(new NewsOrganisation(podcast.Name) { Id = podcast.Id });
        }
    }

    private void ArrangeDestParentReadsBackWhatWasSaved()
    {
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync(() => _savedShow);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync(() => _savedOrganisation);
    }

    private void VerifyDestPlayableUntouched(CatalogueParentKind kind, Guid playableId)
    {
        SavedPlayableIds(kind).Should().NotContain(playableId);
        _mocker.GetMock<ITvShowEpisodeRepository>().Verify(
            r => r.Delete(It.IsAny<Guid>(), playableId), Times.Never);
        _mocker.GetMock<INewsReportRepository>().Verify(
            r => r.Delete(It.IsAny<Guid>(), playableId), Times.Never);
        _mocker.GetMock<IPlayableSearchDocumentSwap>().Verify(
            s => s.DeleteAsync(It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(playableId)), It.IsAny<CancellationToken>()),
            Times.Never);
        (_uploadedSearchDocuments ?? []).Select(d => d.Id).Should().NotContain(playableId.ToString());
    }

    private void VerifyLogged(LogLevel level, string fragment, Func<Times> times) =>
        _mocker.GetMock<ILogger<PodcastKindTransferService>>().Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(fragment)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    private static CosmosException CosmosStatus(HttpStatusCode status) =>
        new("synthetic", status, 0, string.Empty, 0);

    private IReadOnlyList<Guid> SavedPlayableIds(CatalogueParentKind kind)
    {
        if (kind == CatalogueParentKind.TvShow)
        {
            lock (_savedTvEpisodes)
            {
                return _savedTvEpisodes.Select(item => item.Id).ToList();
            }
        }

        lock (_savedReports)
        {
            return _savedReports.Select(item => item.Id).ToList();
        }
    }

    [Theory(DisplayName =
        "While a transfer of a podcast is running, a second transfer request for the same podcast returns " +
        "AlreadyRunning and writes nothing, and the first still completes, because two concurrent transfers of one " +
        "podcast raced each other in production.")]
    [InlineData(CatalogueParentKind.TvShow)]
    [InlineData(CatalogueParentKind.NewsOrganisation)]
    public async Task concurrent_request_is_already_running(CatalogueParentKind kind)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var parentSaveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseParentSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var parentSaves = 0;
        async Task GatedParentSave()
        {
            Interlocked.Increment(ref parentSaves);
            parentSaveEntered.TrySetResult();
            await releaseParentSave.Task;
        }

        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Returns(GatedParentSave);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Returns(GatedParentSave);

        // Act
        var first = Transfer(podcast.Id, kind);
        await parentSaveEntered.Task;
        var second = await Transfer(podcast.Id, kind);
        releaseParentSave.SetResult();
        var firstResult = await first;

        // Assert
        second.Status.Should().Be(PodcastKindTransferStatus.AlreadyRunning);
        second.ParentId.Should().Be(podcast.Id);
        second.TargetKind.Should().Be(kind);
        parentSaves.Should().Be(1);
        firstResult.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        _locks.IsHeld(podcast.Id).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A fresh transfer marker left by another request makes the transfer return AlreadyRunning without reading " +
        "or writing catalogue rows, and the refused request leaves that marker in place.")]
    public async Task fresh_marker_is_already_running_and_writes_nothing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        _locks.SeedMarker(podcast.Id, _now - PodcastKindTransferService.LockStaleAfter / 2);

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.NewsOrganisation);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.AlreadyRunning);
        _savedOrganisation.Should().BeNull();
        _savedReports.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _locks.IsHeld(podcast.Id).Should().BeTrue();
    }

    [Theory(DisplayName =
        "A stale transfer marker (a transfer that died part way) is taken over and the transfer resumes: the " +
        "existing destination parent is reused, the remaining source episodes move, the podcast is deleted, and a " +
        "playable the earlier attempt already moved (its source row gone) is not deleted, rewritten or dropped from " +
        "search.")]
    [InlineData(CatalogueParentKind.TvShow)]
    [InlineData(CatalogueParentKind.NewsOrganisation)]
    public async Task stale_marker_resumes_partial_transfer(CatalogueParentKind kind)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var remainingFirst = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var remainingSecond = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var alreadyMovedId = _fixture.CreateGuid();
        _podcasts.Seed(podcast);
        _episodes.Seed(remainingFirst, remainingSecond);
        ArrangeExistingDestParent(kind, podcast);
        _locks.SeedMarker(podcast.Id, _now - PodcastKindTransferService.LockStaleAfter - TimeSpan.FromMinutes(1), kind);

        // Act
        var result = await Transfer(podcast.Id, kind);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        result.PlayableCount.Should().Be(2);
        SavedPlayableIds(kind).Should().BeEquivalentTo([remainingFirst.Id, remainingSecond.Id]);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, remainingFirst.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, remainingSecond.Id)).Should().BeNull();
        _locks.IsHeld(podcast.Id).Should().BeFalse();
        VerifyDestPlayableUntouched(kind, alreadyMovedId);
        _deletedTvShowIds.Should().BeEmpty();
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
    }

    [Theory(DisplayName =
        "When deleting the source episodes fails part way, the podcast row stays and the marker is released, so " +
        "a retry resumes and completes the transfer with every source row gone, because the podcast is deleted last.")]
    [InlineData(CatalogueParentKind.TvShow)]
    [InlineData(CatalogueParentKind.NewsOrganisation)]
    public async Task retry_after_partial_source_delete_completes(CatalogueParentKind kind)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var first = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var second = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var third = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(first, second, third);
        ArrangeDestParentReadsBackWhatWasSaved();
        _episodes.BeforeDelete = (_, episodeId) =>
        {
            if (episodeId == second.Id)
            {
                throw new InvalidOperationException(_fixture.Create<string>());
            }
        };

        // Act
        var failed = await Transfer(podcast.Id, kind);
        var podcastAfterFailure = await _podcasts.GetPodcast(podcast.Id);
        var markerAfterFailure = _locks.IsHeld(podcast.Id);
        _episodes.BeforeDelete = null;
        var retried = await Transfer(podcast.Id, kind);

        // Assert
        failed.Status.Should().Be(PodcastKindTransferStatus.Failed);
        podcastAfterFailure.Should().NotBeNull();
        markerAfterFailure.Should().BeFalse();
        retried.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        retried.ParentId.Should().Be(podcast.Id);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        foreach (var episode in new[] { first, second, third })
        {
            (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        }

        SavedPlayableIds(kind).Distinct().Should().BeEquivalentTo([first.Id, second.Id, third.Id]);
    }

    [Fact(DisplayName =
        "When a resumed transfer fails writing playables, the destination parent an earlier attempt wrote is not " +
        "deleted, because its already-moved playables would be orphaned.")]
    public async Task resumed_dest_write_failure_keeps_existing_parent()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        ArrangeExistingDestParent(CatalogueParentKind.TvShow, podcast);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.TvShow);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Failed);
        _deletedTvShowIds.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        _locks.IsHeld(podcast.Id).Should().BeFalse();
    }

    [Theory(DisplayName =
        "Source episodes are deleted in one bulk call (no per-episode read, latest-release recompute or counter " +
        "patch) while the podcast row still exists, and the podcast row is deleted after them.")]
    [InlineData(CatalogueParentKind.TvShow)]
    [InlineData(CatalogueParentKind.NewsOrganisation)]
    public async Task source_rows_deleted_in_bulk_then_podcast_last(CatalogueParentKind kind)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episodes = Enumerable.Range(0, 3)
            .Select(_ => _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast))
            .ToArray();
        _podcasts.Seed(podcast);
        _episodes.Seed(episodes);
        var podcastPresentAtEachEpisodeDelete = new List<bool>();
        _episodes.BeforeDelete = (podcastId, _) =>
            podcastPresentAtEachEpisodeDelete.Add(_podcasts.GetPodcast(podcastId).GetAwaiter().GetResult() is not null);

        // Act
        var result = await Transfer(podcast.Id, kind);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        _episodes.BulkDeletes.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(episodes.Select(episode => episode.Id));
        podcastPresentAtEachEpisodeDelete.Should().HaveCount(3).And.OnlyContain(present => present);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Once the transfer starts writing, a cancelled caller token does not stop it: the transfer completes, the " +
        "search swap runs with a token that cannot be cancelled, and the source rows are deleted, because a dropped " +
        "browser connection must not leave a half-moved podcast.")]
    public async Task write_phase_ignores_caller_cancellation()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        using var caller = new CancellationTokenSource();
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show =>
            {
                _savedShow = show;
                caller.Cancel();
            })
            .Returns(Task.CompletedTask);

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.TvShow, caller.Token);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        _savedTvEpisode!.Id.Should().Be(episode.Id);
        _mocker.GetMock<IPlayableSearchDocumentSwap>().Verify(s => s.UploadAsync(
            It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(),
            It.Is<CancellationToken>(token => !token.CanBeCanceled)), Times.Once);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Destination playables are written with bounded parallelism (more than one, at most " +
        "CatalogueBulkWrite.Parallelism at once), so a large transfer does not pay one round trip per episode in turn.")]
    public async Task playable_writes_are_parallel_and_bounded()
    {
        // Arrange
        const int episodeCount = 64;
        var writeLatency = TimeSpan.FromMilliseconds(20);
        var podcast = _fixture.CreatePodcast();
        var episodes = Enumerable.Range(0, episodeCount)
            .Select(_ => _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast))
            .ToArray();
        _podcasts.Seed(podcast);
        _episodes.Seed(episodes);
        var inFlight = 0;
        var maxInFlight = 0;
        var written = 0;
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Returns(async () =>
            {
                var now = Interlocked.Increment(ref inFlight);
                int seen;
                do
                {
                    seen = maxInFlight;
                } while (now > seen && Interlocked.CompareExchange(ref maxInFlight, now, seen) != seen);

                await Task.Delay(writeLatency);
                Interlocked.Decrement(ref inFlight);
                Interlocked.Increment(ref written);
            });

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.NewsOrganisation);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        written.Should().Be(episodeCount);
        maxInFlight.Should().BeGreaterThan(1).And.BeLessThanOrEqualTo(CatalogueBulkWrite.Parallelism);
    }

    [Fact(DisplayName =
        "A cancellation-type exception from the write phase (a Cosmos or HTTP client timeout) after the caller has " +
        "gone is an Error and Failed, not the Information 'cancelled before any write', because rows were written.")]
    public async Task write_phase_timeout_after_caller_gone_is_error()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        using var caller = new CancellationTokenSource();
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(_ => caller.Cancel())
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .ThrowsAsync(new OperationCanceledException(_fixture.Create<string>()));

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.TvShow, caller.Token);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Failed);
        VerifyLogged(LogLevel.Error, "Failed to transfer podcast", Times.Once);
        VerifyLogged(LogLevel.Information, "before any write", Times.Never);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Theory(DisplayName =
        "An episode added to the podcast while the transfer runs (Indexer, submit) is moved too before the podcast " +
        "row is deleted, so it is not orphaned without a parent.")]
    [InlineData(CatalogueParentKind.TvShow)]
    [InlineData(CatalogueParentKind.NewsOrganisation)]
    public async Task episode_arriving_mid_transfer_is_moved_before_podcast_delete(CatalogueParentKind kind)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var late = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var arrived = false;
        _episodes.AfterDeleteMany = _ =>
        {
            if (!arrived)
            {
                arrived = true;
                _episodes.Seed(late);
            }
        };

        // Act
        var result = await Transfer(podcast.Id, kind);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        result.PlayableCount.Should().Be(2);
        SavedPlayableIds(kind).Should().BeEquivalentTo([episode.Id, late.Id]);
        (await _episodes.GetEpisode(podcast.Id, late.Id)).Should().BeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "When episodes keep arriving past the extra passes, the podcast row is kept and the transfer returns " +
        "Incomplete, so nothing is orphaned and a re-run finishes it.")]
    public async Task episodes_still_arriving_keep_podcast_and_return_incomplete()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        _episodes.Seed(_fixture.CreateStoredEpisodeWithYouTubeOnly(podcast));
        _episodes.AfterDeleteMany = _ => _episodes.Seed(_fixture.CreateStoredEpisodeWithYouTubeOnly(podcast));

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.NewsOrganisation);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Incomplete);
        _episodes.BulkDeletes.Should().HaveCount(PodcastKindTransferService.MaxLateArrivalPasses + 1);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
        _locks.IsHeld(podcast.Id).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A transfer that runs past the heartbeat interval renews its marker while it works, so a long transfer is " +
        "not taken over as stale while it is still running.")]
    public async Task long_transfer_renews_marker()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episodes = Enumerable.Range(0, 20)
            .Select(_ => _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast))
            .ToArray();
        _podcasts.Seed(podcast);
        _episodes.Seed(episodes);
        _clockStep = PodcastKindTransferService.LockHeartbeatInterval / 2;

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.TvShow);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        _locks.Renewals.Should().NotBeEmpty();
        _locks.IsHeld(podcast.Id).Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a heartbeat finds the marker taken over by another request, this attempt stops writing at once: no " +
        "source rows or podcast are deleted and no destination rows are compensated away, because the new holder " +
        "owns them; it returns AlreadyRunning.")]
    public async Task lost_marker_stops_without_compensating()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episodes = Enumerable.Range(0, 40)
            .Select(_ => _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast))
            .ToArray();
        _podcasts.Seed(podcast);
        _episodes.Seed(episodes);
        var stolen = 0;
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Returns(async () =>
            {
                if (Interlocked.Exchange(ref stolen, 1) == 0)
                {
                    _locks.StealMarker(podcast.Id, _now);
                    lock (_locks)
                    {
                        _now += PodcastKindTransferService.LockHeartbeatInterval * 2;
                    }
                }

                await Task.Delay(5);
            });

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.NewsOrganisation);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.AlreadyRunning);
        _episodes.BulkDeletes.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        _mocker.GetMock<INewsReportRepository>().Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
        _locks.IsHeld(podcast.Id).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A destination write throttled (429) after the SDK's own retries is retried and the transfer completes, " +
        "instead of failing part way on one hot partition.")]
    public async Task throttled_playable_write_is_retried()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var attempts = 0;
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Returns(() => Interlocked.Increment(ref attempts) == 1
                ? Task.FromException(CosmosStatus(HttpStatusCode.TooManyRequests))
                : Task.CompletedTask);

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.TvShow);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Accepted);
        attempts.Should().Be(2);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Taking over a stale marker of a transfer to the other kind, then finding that kind's parent, is a Conflict " +
        "with a Warning naming the kind to re-run, so the curator can finish the earlier transfer.")]
    public async Task stale_marker_of_other_kind_names_the_kind_to_rerun()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        _episodes.Seed(_fixture.CreateStoredEpisodeWithYouTubeOnly(podcast));
        ArrangeExistingDestParent(CatalogueParentKind.NewsOrganisation, podcast);
        _locks.SeedMarker(
            podcast.Id,
            _now - PodcastKindTransferService.LockStaleAfter - TimeSpan.FromMinutes(1),
            CatalogueParentKind.NewsOrganisation);

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.TvShow);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Conflict);
        VerifyLogged(LogLevel.Warning, "did not finish", Times.Once);
        _savedShow.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a fresh transfer's playable write fails, the playables it wrote are compensated in parallel and a dest " +
        "row already gone (404) is not a failure, and the new parent is removed.")]
    public async Task compensation_tolerates_already_gone_rows()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episodes = Enumerable.Range(0, 10)
            .Select(_ => _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast))
            .ToArray();
        _podcasts.Seed(podcast);
        _episodes.Seed(episodes);
        var saves = 0;
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Returns(async () =>
            {
                await Task.Delay(1);
                if (Interlocked.Increment(ref saves) == episodes.Length)
                {
                    throw new InvalidOperationException(_fixture.Create<string>());
                }
            });
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));

        // Act
        var result = await Transfer(podcast.Id, CatalogueParentKind.TvShow);

        // Assert
        result.Status.Should().Be(PodcastKindTransferStatus.Failed);
        _mocker.GetMock<ITvShowEpisodeRepository>().Verify(
            r => r.Delete(podcast.Id, It.IsAny<Guid>()), Times.Exactly(episodes.Length - 1));
        _deletedTvShowIds.Should().ContainSingle().Which.Should().Be(podcast.Id);
        VerifyLogged(LogLevel.Warning, "Failed to compensate", Times.Never);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }
}
