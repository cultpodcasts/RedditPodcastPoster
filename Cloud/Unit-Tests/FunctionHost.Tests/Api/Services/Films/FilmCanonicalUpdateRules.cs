using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Films;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Films;

public class FilmCanonicalUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private Film? _saved;

    public FilmCanonicalUpdateRules()
    {
        _mocker.Use(NullLogger<FilmUpdateService>.Instance);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(film => _saved = film)
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "POST film stores IMDb on IFilmCanonical, because several films share a display name and IMDb is the identity page.")]
    public async Task update_sets_imdb()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var imdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var sut = _mocker.CreateInstance<FilmUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { Imdb = imdb.ToString() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Imdb.Should().Be(imdb);
    }

    [Fact(DisplayName =
        "POST film returns NotFound when the film id is missing, because identity links are stored on the film document.")]
    public async Task update_not_found_when_missing()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(filmId))
            .ReturnsAsync((Film?)null);
        var sut = _mocker.CreateInstance<FilmUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(
                filmId,
                new FilmChangeRequest { Imdb = $"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }
}
