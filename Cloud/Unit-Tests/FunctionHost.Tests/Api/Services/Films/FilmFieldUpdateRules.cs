using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Films;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Films;

public class FilmFieldUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private Film? _saved;

    public FilmFieldUpdateRules()
    {
        _mocker.Use(NullLogger<FilmService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(film => _saved = film)
            .Returns(Task.CompletedTask);
    }

    private Film CreateFilm(Action<Film>? customise = null)
    {
        var film = new Film("Synthetic Cult Film") { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        film.SetRelease(CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 27));
        film.Subjects = ["Synthetic Cult"];
        customise?.Invoke(film);
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetFilm(film.Id)).ReturnsAsync(film);
        return film;
    }

    private Task<FilmUpdateResult> Update(Film film, FilmChangeRequest change) =>
        _mocker.CreateInstance<FilmService>()
            .UpdateAsync(new FilmChangeRequestWrapper(film.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH film sets publisher and playable fields it is given and keeps omitted ones and IMDb, because a film is a publisher that is also playable.")]
    public async Task update_sets_fields()
    {
        // Arrange
        var imdb = new Uri("https://www.imdb.com/title/tt0000003/");
        var film = CreateFilm(f => { f.Imdb = imdb; f.TwitterHandle = "@keep"; });
        var duration = _fixture.CreateDuration() + TimeSpan.FromSeconds(30);

        // Act
        var result = await Update(film, new FilmChangeRequest
        {
            Description = "Synthetic film description",
            Duration = duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture),
            Explicit = true,
            Ignored = true,
            SearchTerms = "synthetic film",
            HashTag = "#Film",
            Language = "it",
            Subjects = ["Synthetic New"],
            Guests = [" Synthetic Director ", ""]
        });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Description.Should().Be("Synthetic film description");
        _saved.Length.Should().Be(duration);
        _saved.Explicit.Should().BeTrue();
        _saved.Ignored.Should().BeTrue();
        _saved.SearchTerms.Should().Be("synthetic film");
        _saved.HashTag.Should().Be("#Film");
        _saved.Language.Should().Be("it");
        _saved.Subjects.Should().Equal("Synthetic New");
        _saved.RemovedSubjects.Should().Contain("Synthetic Cult");
        _saved.Guests.Should().Equal("Synthetic Director");
        _saved.TwitterHandle.Should().Be("@keep");
        _saved.Imdb.Should().Be(imdb);
        _saved.Release!.Precision.Should().Be(CatalogueReleasePrecision.Year);
        _search.VerifyDelete(film.Id, Times.Never());
    }

    [Theory(DisplayName =
        "PATCH film release accepts a year or a calendar date, because film releases are often only known to the year.")]
    [InlineData("2001", CatalogueReleasePrecision.Year)]
    [InlineData("2001-05-04", CatalogueReleasePrecision.Date)]
    public async Task update_release_year_or_date(string release, CatalogueReleasePrecision precision)
    {
        // Arrange
        var film = CreateFilm();

        // Act
        await Update(film, new FilmChangeRequest { Release = release });

        // Assert
        _saved!.Release!.Precision.Should().Be(precision);
        _saved.Release.Year.Should().Be(2001);
    }

    [Theory(DisplayName =
        "PATCH film with an invalid release or negative duration is a 400 and nothing is applied, because the request is validated first.")]
    [InlineData("2001-05-04T10:00:00", null)]
    [InlineData("01", null)]
    [InlineData(null, "-0:01:00")]
    public async Task update_rejects_invalid(string? release, string? duration)
    {
        // Arrange
        var film = CreateFilm();

        // Act
        var result = await Update(film, new FilmChangeRequest { Release = release, Duration = duration, HashTag = "#Nope" });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.BadRequest);
        _saved.Should().BeNull();
        film.HashTag.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH film removed=true deletes its search document and [] clears guests, because the film indexer never deletes.")]
    public async Task update_removed_and_clear_guests()
    {
        // Arrange
        var film = CreateFilm(f => f.Guests = ["Synthetic Old"]);

        // Act
        await Update(film, new FilmChangeRequest { Removed = true, Guests = [] });

        // Assert
        _saved!.IsRemoved().Should().BeTrue();
        _saved.Guests.Should().BeNull();
        _search.VerifyDelete(film.Id, Times.Once());
    }

    [Fact(DisplayName =
        "GET film DTO carries publisher and playable fields with a year-only release as yyyy, because the curator UI edits them.")]
    public void dto_has_fields()
    {
        // Arrange
        var film = CreateFilm(f => { f.Guests = ["Synthetic Director"]; f.HashTag = "#Film"; });

        // Act
        var dto = film.ToDto();

        // Assert
        dto.Name.Should().Be("Synthetic Cult Film");
        dto.Release.Should().Be(film.Release!.Year.ToString(CultureInfo.InvariantCulture));
        dto.Duration.Should().Be(film.Length.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture));
        dto.HashTag.Should().Be("#Film");
        dto.Subjects.Should().Equal("Synthetic Cult");
        dto.Guests.Should().Equal("Synthetic Director");
    }
}
