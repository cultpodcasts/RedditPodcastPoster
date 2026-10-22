using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Films;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Films;

public class FilmLinkUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<EpisodeSearchRecord> _uploads = [];
    private Film? _saved;

    public FilmLinkUpdateRules()
    {
        _mocker.Use(NullLogger<FilmService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(film => _saved = film)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploads.AddRange(documents))
            .ReturnsAsync(true);
    }

    private Film CreateFilm(Action<Film>? customise = null)
    {
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        customise?.Invoke(film);
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetFilm(film.Id)).ReturnsAsync(film);
        return film;
    }

    private FilmTrailer CreateTrailer() => new()
    {
        Id = _fixture.CreateGuid(),
        Title = _fixture.CreateTitle(),
        Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
        Length = _fixture.CreateDuration()
    };

    private Task<FilmUpdateResult> Update(Film film, FilmChangeRequest change) =>
        _mocker.CreateInstance<FilmService>()
            .UpdateAsync(new FilmChangeRequestWrapper(film.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH film imdbId and tmdbId set the ids, and an empty imdbId or a zero tmdbId clears them.")]
    public async Task ids_set_and_clear()
    {
        // Arrange
        var film = CreateFilm();

        // Act
        var set = await Update(film, new FilmChangeRequest { ImdbId = " tt0000003 ", TmdbId = 7 });
        var setImdb = _saved!.ImdbId;
        var setTmdb = _saved.TmdbId;
        var cleared = await Update(film, new FilmChangeRequest { ImdbId = "", TmdbId = 0 });

        // Assert
        set.Status.Should().Be(FilmUpdateStatus.Accepted);
        setImdb.Should().Be("tt0000003");
        setTmdb.Should().Be(7);
        cleared.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved.ImdbId.Should().BeNull();
        _saved.TmdbId.Should().BeNull();
    }

    [Theory(DisplayName =
        "PATCH film rejects an IMDb id that is not tt plus 7 to 10 digits, and a negative TMDB id, without saving.")]
    [InlineData("nm0000003", null)]
    [InlineData("tt123", null)]
    [InlineData(null, -1)]
    public async Task invalid_ids_are_rejected(string? imdbId, int? tmdbId)
    {
        // Arrange
        var film = CreateFilm();

        // Act
        var result = await Update(film, new FilmChangeRequest { ImdbId = imdbId, TmdbId = tmdbId });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH film services adds a link under the key its URL resolves to and removes a key sent with no URL or image.")]
    public async Task services_add_and_remove()
    {
        // Arrange
        var oldKey = "internetArchive";
        var film = CreateFilm(f => f.Services = new Dictionary<string, ServiceLink>
        {
            [oldKey] = new() { Url = new Uri($"https://archive.org/details/synthetic-{_fixture.CreateGuid():N}") }
        });
        var youTube = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

        // Act
        var result = await Update(film, new FilmChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                ["anything"] = new() { Url = youTube },
                [oldKey] = new() { Url = new Uri("", UriKind.RelativeOrAbsolute) }
            }
        });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Services.Should().ContainSingle();
        _saved.Services!["youtube"].Url.Should().Be(youTube);
    }

    [Fact(DisplayName =
        "PATCH film services rejects a relative URL, because a film link must open on its own.")]
    public async Task services_reject_relative_url()
    {
        // Arrange
        var film = CreateFilm();

        // Act
        var result = await Update(film, new FilmChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                ["youtube"] = new() { Url = new Uri("watch?v=synthetic", UriKind.Relative) }
            }
        });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH film trailers keeps the listed trailers in the given order, renames them, and drops the rest.")]
    public async Task trailers_reorder_rename_and_remove()
    {
        // Arrange
        var first = CreateTrailer();
        var second = CreateTrailer();
        var dropped = CreateTrailer();
        var film = CreateFilm(f => f.Trailers = [first, second, dropped]);
        var newTitle = _fixture.CreateTitle();

        // Act
        var result = await Update(film, new FilmChangeRequest
        {
            Trailers = [new FilmTrailerChange(second.Id, $" {newTitle} "), new FilmTrailerChange(first.Id, null)]
        });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Trailers!.Select(t => t.Id).Should().Equal(second.Id, first.Id);
        _saved.Trailers![0].Title.Should().Be(newTitle);
        _uploads.Should().ContainSingle().Which.Trailers.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "PATCH film trailers rejects an id the film does not have, so a stale editor cannot invent a trailer.")]
    public async Task trailers_reject_unknown_id()
    {
        // Arrange
        var film = CreateFilm(f => f.Trailers = [CreateTrailer()]);

        // Act
        var result = await Update(film, new FilmChangeRequest
        {
            Trailers = [new FilmTrailerChange(_fixture.CreateGuid(), null)]
        });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.BadRequest);
        _saved.Should().BeNull();
        film.Trailers.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Removing a film's last trailer pushes an empty trailers list, because search merge ignores an omitted field.")]
    public async Task removing_last_trailer_clears_search_field()
    {
        // Arrange
        var film = CreateFilm(f => f.Trailers = [CreateTrailer()]);

        // Act
        await Update(film, new FilmChangeRequest { Trailers = [] });

        // Assert
        _saved!.Trailers.Should().BeNull();
        _uploads.Should().ContainSingle().Which.Trailers.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName =
        "An edit of a film that never had trailers omits the trailers field, so films still push before the index has it.")]
    public async Task film_without_trailers_omits_field()
    {
        // Arrange
        var film = CreateFilm();

        // Act
        await Update(film, new FilmChangeRequest { Explicit = true });

        // Assert
        _uploads.Should().ContainSingle().Which.Trailers.Should().BeNull();
    }
}
