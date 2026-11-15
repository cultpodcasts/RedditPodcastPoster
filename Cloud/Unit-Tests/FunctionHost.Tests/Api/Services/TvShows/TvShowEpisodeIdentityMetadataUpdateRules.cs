using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.TvShows;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Tmdb.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

/// <summary>RPP #1430: a TV episode PATCH fills the episode's empty fields from its identity, never overwriting.</summary>
public class TvShowEpisodeIdentityMetadataUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private TvShowEpisode? _saved;

    public TvShowEpisodeIdentityMetadataUpdateRules()
    {
        _mocker.Use(NullLogger<TvShowEpisodeService>.Instance);
        _mocker.Use(new SearchCleanupMock().Cleanup);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "Setting a TV episode's TheTVDB id fills its empty overview, air date, runtime and still from TMDB, using the " +
        "parent show's TMDB series id, and keeps the episode name it already has.")]
    public async Task setting_ids_fills_only_empty_fields()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), TmdbId = _fixture.CreateTmdbId() };
        var episode = Stored(new TvShowEpisode(title) { TvShowId = show.Id });
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        var metadata = new TmdbEpisodeMetadata(
            _fixture.CreateTmdbId(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            47,
            new DateOnly(2019, 2, 3),
            new Uri("https://image.tmdb.org/t/p/w780/still.jpg"));
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchTvShowEpisodeAsync(episode, show.TmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(
                episode.Id,
                new TvShowEpisodeChangeRequest { TvdbId = _fixture.CreateTvdbId() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.Title.Should().Be(title);
        _saved.Description.Should().Be(metadata.Overview);
        _saved.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(metadata.AirDate!.Value));
        _saved.Length.Should().Be(TimeSpan.FromMinutes(47));
        _saved.Image.Should().Be(metadata.Still);
        _saved.TmdbId.Should().Be(metadata.Id);
    }

    [Fact(DisplayName =
        "Clearing a TV episode's overview in the same PATCH leaves it empty even when TMDB has one; other empty fields " +
        "are still filled.")]
    public async Task a_description_cleared_in_this_patch_is_not_refilled()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), TmdbId = _fixture.CreateTmdbId() };
        var episode = Stored(new TvShowEpisode(title)
        {
            TvShowId = show.Id,
            TvdbId = _fixture.CreateTvdbId(),
            Description = _fixture.CreateTitle()
        });
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        var metadata = new TmdbEpisodeMetadata(
            _fixture.CreateTmdbId(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            47,
            DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(40)),
            new Uri("https://image.tmdb.org/t/p/w780/still.jpg"));
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchTvShowEpisodeAsync(episode, show.TmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(episode.Id, new TvShowEpisodeChangeRequest { Description = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.Title.Should().Be(title);
        _saved.Description.Should().BeEmpty();
        _saved.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(metadata.AirDate!.Value));
        _saved.Length.Should().Be(TimeSpan.FromMinutes(47));
        _saved.Image.Should().Be(metadata.Still);
    }

    [Fact(DisplayName =
        "A TV episode with every field set and unchanged ids makes no identity lookup, so routine edits cost no TMDB call.")]
    public async Task complete_episode_makes_no_lookup()
    {
        // Arrange
        var episode = Stored(new TvShowEpisode(_fixture.CreateTitle())
        {
            TvdbId = _fixture.CreateTvdbId(),
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            Image = new Uri("https://image.tmdb.org/t/p/w780/still.jpg")
        });
        episode.SetRelease(CatalogueRelease.FromYear(2020));
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(episode.Id, new TvShowEpisodeChangeRequest { Explicit = true }),
            CancellationToken.None);

        // Assert
        _mocker.GetMock<IIdentityMetadataEnricher>().Verify(
            e => e.FetchTvShowEpisodeAsync(It.IsAny<TvShowEpisode>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private TvShowEpisode Stored(TvShowEpisode episode)
    {
        episode.Id = _fixture.CreateGuid();
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        return episode;
    }
}
