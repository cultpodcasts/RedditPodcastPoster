using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Films;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Films;

public class FilmDeleteRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<PodcastEpisode> _shortLinkDeletes = [];

    public FilmDeleteRules()
    {
        _mocker.Use(NullLogger<FilmService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(target => _shortLinkDeletes.Add(target))
            .ReturnsAsync(new RedditPodcastPoster.Cloudflare.Models.DeleteResult(true));
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
    }

    private Film CreateFilm(Action<Film>? customise = null)
    {
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        film.SetRelease(CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 5));
        customise?.Invoke(film);
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetFilm(film.Id)).ReturnsAsync(film);
        return film;
    }

    [Fact(DisplayName =
        "Delete film returns NotFound when the film is missing, because nothing can be removed.")]
    public async Task delete_returns_not_found_when_missing()
    {
        // Arrange
        var missingId = _fixture.CreateGuid();
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetFilm(missingId)).ReturnsAsync((Film?)null);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.DeleteAsync(missingId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.NotFound);
        _mocker.GetMock<IFilmRepository>().Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "Delete film returns AlreadySocial when the film is tweeted, because social posts must be cleared first.")]
    public async Task delete_returns_already_social_when_tweeted()
    {
        // Arrange
        var film = CreateFilm(f => f.OldTweeted = true);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.DeleteAsync(film.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.AlreadySocial);
        result.Tweeted.Should().BeTrue();
        _mocker.GetMock<IFilmRepository>().Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "Delete film removes the Cosmos row when the film is not tweeted, so an admin can finish soft-remove with a hard delete.")]
    public async Task delete_success_deletes_film()
    {
        // Arrange
        var film = CreateFilm();
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.DeleteAsync(film.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.Deleted);
        _mocker.GetMock<IFilmRepository>().Verify(r => r.Delete(film.Id), Times.Once);
        _search.VerifyDelete(film.Id, Times.Once());
        _shortLinkDeletes.Should().ContainSingle().Which.Episode.Id.Should().Be(film.Id);
    }
}
