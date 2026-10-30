using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

public class TvShowEpisodeCanonicalUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private TvShowEpisode? _saved;

    public TvShowEpisodeCanonicalUpdateRules()
    {
        _mocker.Use(NullLogger<TvShowEpisodeService>.Instance);
        _mocker.Use(new SearchCleanupMock().Cleanup);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "POST TV-show episode stores IMDb, TheTVDB and TMDB ids on the playable, because episode identity is the ids not streaming services.")]
    public async Task update_sets_imdb_tvdb_and_tmdb_ids()
    {
        // Arrange
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var imdbId = _fixture.CreateImdbId();
        var tvdbId = _fixture.CreateTvdbId();
        var tmdbId = _fixture.CreateTmdbId();
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(
                episode.Id,
                new TvShowEpisodeChangeRequest { ImdbId = imdbId, TvdbId = tvdbId, TmdbId = tmdbId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved.Should().NotBeNull();
        _saved!.ImdbId.Should().Be(imdbId);
        _saved.TvdbId.Should().Be(tvdbId);
        _saved.TmdbId.Should().Be(tmdbId);
    }

    [Fact(DisplayName =
        "POST TV-show episode with an empty IMDb id clears IMDb and leaves TVDB, because omitted JSON is a no-op.")]
    public async Task update_clears_imdb_id_when_empty_string()
    {
        // Arrange
        var existingTvdbId = _fixture.CreateTvdbId();
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = _fixture.CreateImdbId(),
            TvdbId = existingTvdbId
        };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(
                episode.Id,
                new TvShowEpisodeChangeRequest { ImdbId = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.ImdbId.Should().BeNull();
        _saved.TvdbId.Should().Be(existingTvdbId);
    }

    [Fact(DisplayName =
        "POST TV-show episode returns BadRequest when the IMDb id is not tt followed by digits, because the id is the identity and a URL is not an id.")]
    public async Task update_rejects_malformed_imdb_id()
    {
        // Arrange
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(
                episode.Id,
                new TvShowEpisodeChangeRequest { ImdbId = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET TV-show episode by id returns Found with the identity ids, because curator GET cannot use the parent partition key.")]
    public async Task get_by_id_returns_identity_ids()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = imdbId
        };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.GetAsync(episode.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeGetStatus.Found);
        result.Episode.Should().NotBeNull();
        result.Episode!.ImdbId.Should().Be(imdbId);
    }
}