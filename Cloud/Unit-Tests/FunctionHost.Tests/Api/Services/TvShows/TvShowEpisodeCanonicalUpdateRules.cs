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
        _mocker.Use(NullLogger<TvShowEpisodeUpdateService>.Instance);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "POST TV-show episode stores IMDb and TheTVDB URIs on the playable, because episode identity pages are not streaming services.")]
    public async Task update_sets_imdb_and_tvdb()
    {
        // Arrange
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var imdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var tvdb = new Uri($"https://www.thetvdb.com/series/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<TvShowEpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(
                episode.Id,
                new TvShowEpisodeChangeRequest { Imdb = imdb.ToString(), Tvdb = tvdb.ToString() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved.Should().NotBeNull();
        _saved!.Imdb.Should().Be(imdb);
        _saved.Tvdb.Should().Be(tvdb);
    }

    [Fact(DisplayName =
        "POST TV-show episode with an empty IMDb string clears IMDb and leaves TVDB, because omitted JSON is a no-op.")]
    public async Task update_clears_imdb_when_empty_string()
    {
        // Arrange
        var existingImdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var existingTvdb = new Uri($"https://www.thetvdb.com/series/{_fixture.CreateYouTubeId()}");
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Imdb = existingImdb,
            Tvdb = existingTvdb
        };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var sut = _mocker.CreateInstance<TvShowEpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(
                episode.Id,
                new TvShowEpisodeChangeRequest { Imdb = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.Imdb.Should().BeNull();
        _saved.Tvdb.Should().Be(existingTvdb);
    }

    [Fact(DisplayName =
        "POST TV-show episode returns BadRequest when IMDb is not an absolute http(s) URL, because identity links must be pasteable pages.")]
    public async Task update_rejects_relative_imdb()
    {
        // Arrange
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var sut = _mocker.CreateInstance<TvShowEpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(
                episode.Id,
                new TvShowEpisodeChangeRequest { Imdb = "not-a-url" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET TV-show episode by id returns Found with ITvCanonical URIs, because curator GET cannot use the parent partition key.")]
    public async Task get_by_id_returns_canonical_uris()
    {
        // Arrange
        var imdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Imdb = imdb
        };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var getService = _mocker.CreateInstance<TvShowEpisodeGetService>();

        // Act
        var result = await getService.GetAsync(episode.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeGetStatus.Found);
        result.Episode.Should().NotBeNull();
        result.Episode!.Imdb.Should().Be(imdb);
    }
}
