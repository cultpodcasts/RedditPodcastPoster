using Api.Models;
using Api.Services.News;
using Api.Services.Podcasts;
using Api.Services.TvShows;
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
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

/// <summary>
/// YouTube indexing hints are owned by the indexer. A curator edit loads the stored document, changes the fields it
/// was given and saves it, so the hints it loaded are saved back unchanged.
/// </summary>
public class CuratorSaveKeepsYouTubeIndexingHintsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly YouTubeIndexingHints _hints;
    private TvShow? _savedShow;
    private NewsOrganisation? _savedOrganisation;

    public CuratorSaveKeepsYouTubeIndexingHintsRules()
    {
        _hints = new YouTubeIndexingHints
        {
            PlaylistId = _fixture.CreateYouTubePlaylistId(),
            LastFullWalk = DomainTestFixture.UtcDaysAgo(1),
            PlaylistItemCount = 400,
            UploadsCheckedThrough = DomainTestFixture.UtcDaysAgo(0)
        };
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show => _savedShow = show)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(organisation => _savedOrganisation = organisation)
            .Returns(Task.CompletedTask);
    }

    private CatalogueIndexingSource IndexingWithHints() => new()
    {
        YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
        YouTubePlaylistId = _hints.PlaylistId,
        YouTubeIndexingHints = _hints.Copy()
    };

    [Fact(DisplayName =
        "A curator TV show save that changes a service id keeps the indexer's YouTube indexing hints, because a " +
        "curator edit must not throw away what the indexer learned about the playlist.")]
    public async Task tv_show_save_keeps_hints()
    {
        // Arrange
        var show = new TvShow("Synthetic Cult Docuseries") { Id = _fixture.CreateGuid(), Indexing = IndexingWithHints() };
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        var change = new TvShowChangeRequest { YouTubeChannelId = _fixture.CreateYouTubeChannelId() };

        // Act
        await _mocker.CreateInstance<TvShowService>()
            .UpdateAsync(new TvShowChangeRequestWrapper(show.Id, change), CancellationToken.None);

        // Assert
        _savedShow!.Indexing!.YouTubeChannelId.Should().Be(change.YouTubeChannelId);
        _savedShow.Indexing.YouTubeIndexingHints.Should().Be(_hints);
    }

    [Fact(DisplayName =
        "A curator news organisation save that changes a service id keeps the indexer's YouTube indexing hints.")]
    public async Task news_organisation_save_keeps_hints()
    {
        // Arrange
        var organisation = new NewsOrganisation("Synthetic Newsroom")
            { Id = _fixture.CreateGuid(), Indexing = IndexingWithHints() };
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        var change = new NewsOrganisationChangeRequest { SpotifyId = _fixture.CreateSpotifyId() };

        // Act
        await _mocker.CreateInstance<NewsOrganisationService>()
            .UpdateAsync(new NewsOrganisationChangeRequestWrapper(organisation.Id, change), CancellationToken.None);

        // Assert
        _savedOrganisation!.Indexing!.SpotifyId.Should().Be(change.SpotifyId);
        _savedOrganisation.Indexing.YouTubeIndexingHints.Should().Be(_hints);
    }

    [Fact(DisplayName =
        "A curator podcast change, even one that replaces the YouTube playlist id, leaves the stored hints alone; " +
        "hints keyed to the old playlist are ignored by the indexer rather than deleted.")]
    public void podcast_change_keeps_hints()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = _hints.PlaylistId!);
        podcast.YouTubeIndexingHints = _hints.Copy();
        var request = new PodcastChangeRequest { YouTubePlaylistId = _fixture.CreateYouTubePlaylistId() };
        var applier = new PodcastChangeApplier(NullLogger<PodcastChangeApplier>.Instance);

        // Act
        applier.Apply(podcast, request);

        // Assert
        podcast.YouTubePlaylistId.Should().Be(request.YouTubePlaylistId);
        podcast.YouTubeIndexingHints.Should().Be(_hints);
        podcast.YouTubeIndexing.CurrentYouTubeIndexingHints().Should().BeNull();
    }
}
