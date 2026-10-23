using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Films;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
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
        _mocker.GetMock<IFilmRepository>().ReplaceThroughSave();
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
        var result = await Update(film, Seen(film, new FilmChangeRequest
        {
            Trailers = [new FilmTrailerChange(second.Id, $" {newTitle} "), new FilmTrailerChange(first.Id, null)]
        }));

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
        var trailer = CreateTrailer();
        var film = CreateFilm(f => f.Trailers = [trailer]);

        // Act
        await Update(film, Seen(film, new FilmChangeRequest { Trailers = [] }));

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

    [Fact(DisplayName =
        "PATCH film keeps a trailer attached after it read the film, and retries on an ETag conflict, so an edit that is not a trailer keep-list never drops a trailer whose episode is already deleted.")]
    public async Task patch_keeps_trailer_attached_during_the_edit()
    {
        // Arrange
        var kept = CreateTrailer();
        var removed = CreateTrailer();
        var film = CreateFilm(f => f.Trailers = [kept, removed]);
        var attached = CreateTrailer();
        var repository = _mocker.GetMock<IFilmRepository>();
        repository
            .Setup(r => r.GetFilmForUpdate(film.Id))
            .ReturnsAsync(() => new FilmVersion(
                new Film(film.Name) { Id = film.Id, Trailers = [kept, removed, attached] },
                _fixture.CreateTitle()));
        var replaced = new List<Film>();
        repository
            .Setup(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()))
            .Callback<Film, string>((f, _) => replaced.Add(f))
            .ReturnsAsync(() => replaced.Count > 1); // the first attempt loses an ETag race

        // Act
        var result = await Update(film, new FilmChangeRequest { Explicit = true });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        replaced.Should().HaveCount(2);
        var saved = replaced[^1];
        saved.Explicit.Should().BeTrue();
        saved.Trailers!.Select(t => t.Id).Should().Equal(kept.Id, removed.Id, attached.Id);
        _saved.Should().BeNull();
        _uploads.Should().ContainSingle().Which.Trailers.Should().HaveCount(3);
    }

    [Fact(DisplayName =
        "PATCH film that is not a trailer keep-list keeps a rename and a tweeted flag that landed after the edit read the film, because the replace writes that newer film with this edit applied on top.")]
    public async Task non_trailer_patch_keeps_a_rename_and_tweet_that_landed_during_the_edit()
    {
        // Arrange
        var trailer = CreateTrailer();
        var film = CreateFilm(f => f.Trailers = [trailer]);
        var renamed = _fixture.CreateTitle();
        var repository = _mocker.GetMock<IFilmRepository>();
        repository
            .Setup(r => r.GetFilmForUpdate(film.Id))
            .ReturnsAsync(() => new FilmVersion(
                new Film(renamed) { Id = film.Id, Trailers = [trailer], Tweeted = true },
                _fixture.Create<string>()));
        var replaced = new List<Film>();
        repository
            .Setup(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()))
            .Callback<Film, string>((f, _) => replaced.Add(f))
            .ReturnsAsync(true);

        // Act
        var result = await Update(film, new FilmChangeRequest { Explicit = true });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        replaced.Should().ContainSingle();
        var saved = replaced[0];
        saved.Name.Should().Be(renamed);
        saved.Tweeted.Should().BeTrue();
        saved.Explicit.Should().BeTrue();
        saved.Trailers.Should().ContainSingle().Which.Id.Should().Be(trailer.Id);
        _saved.Should().BeNull();
        _uploads.Should().ContainSingle().Which.Title.Should().Be(renamed);
    }

    [Fact(DisplayName =
        "PATCH film that clears a post does not delete the tweet or the Bluesky post when a trailer is attached after the keep-list was checked, because a conflict leaves the live posts and the stored flags together.")]
    public async Task trailer_attached_during_a_keep_list_does_not_retract_social_posts()
    {
        // Arrange
        var kept = CreateTrailer();
        var film = CreateFilm(f =>
        {
            f.Trailers = [kept];
            f.Tweeted = true;
            f.BlueskyPost = _fixture.Create<string>();
        });
        var attached = CreateTrailer();
        var reads = 0;
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilmForUpdate(film.Id))
            .ReturnsAsync(() =>
            {
                reads++;
                var trailers = reads == 1
                    ? new List<FilmTrailer> { kept }
                    : new List<FilmTrailer> { kept, attached };
                return new FilmVersion(
                    new Film(film.Name) { Id = film.Id, Trailers = trailers, Tweeted = true, BlueskyPost = film.BlueskyPost },
                    _fixture.Create<string>());
            });

        // Act
        var result = await Update(film, Seen(film, new FilmChangeRequest
        {
            Trailers = [new FilmTrailerChange(kept.Id, null)],
            Tweeted = false,
            UnBluesky = true
        }));

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Conflict);
        result.UnexpectedTrailerIds.Should().Equal(attached.Id);
        _saved.Should().BeNull();
        _uploads.Should().BeEmpty();
        var social = _mocker.GetMock<IPlayableSocialRetraction>();
        social.Verify(
            retraction => retraction.RemoveTweet(
                It.IsAny<PostStatusChange>(), It.IsAny<IPromotable>(), It.IsAny<SocialPostRemovalTarget>()),
            Times.Never);
        social.Verify(
            retraction => retraction.RemoveBlueskyPost(
                It.IsAny<PostStatusChange>(), It.IsAny<IPromotable>(), It.IsAny<SocialPostRemovalTarget>()),
            Times.Never);
        _mocker.GetMock<IFilmRepository>().Verify(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "PATCH film clears tweeted on a re-read after the replace, so a rename that lands during the delete stays and the replace itself still has the tweet flag.")]
    public async Task untweet_clears_the_flag_on_a_reread_after_the_replace()
    {
        // Arrange
        var film = CreateFilm(f => f.Tweeted = true);
        var renamed = _fixture.CreateTitle();
        var repository = _mocker.GetMock<IFilmRepository>();
        var replacedTweeted = new List<bool>();
        repository
            .Setup(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()))
            .Callback<Film, string>((f, _) => replacedTweeted.Add(f.Tweeted))
            .ReturnsAsync(true);
        _mocker.GetMock<IPlayableSocialRetraction>()
            .Setup(retraction => retraction.RemoveTweet(
                It.IsAny<PostStatusChange>(), It.IsAny<IPromotable>(), It.IsAny<SocialPostRemovalTarget>()))
            .ReturnsAsync(true);
        Film? cleared = null;
        repository
            .Setup(r => r.Update(film.Id, It.IsAny<Func<Film, bool>>()))
            .ReturnsAsync((Guid _, Func<Film, bool> mutate) =>
            {
                var stored = new Film(renamed) { Id = film.Id, Tweeted = true };
                mutate(stored);
                cleared = stored;
                return stored;
            });

        // Act
        var result = await Update(film, new FilmChangeRequest { Tweeted = false });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        result.Outcome!.TweetDeleted.Should().BeTrue();
        replacedTweeted.Should().Equal(true);
        cleared!.Name.Should().Be(renamed);
        cleared.Tweeted.Should().BeFalse();
        _uploads.Should().ContainSingle().Which.Title.Should().Be(renamed);
    }

    [Fact(DisplayName =
        "PATCH film trailers is a conflict when the film has a trailer the client did not list, and nothing is saved, because a stale keep-list must not delete a trailer whose episode is already gone.")]
    public async Task stale_trailer_keep_list_is_conflict()
    {
        // Arrange
        var kept = CreateTrailer();
        var attached = CreateTrailer();
        var film = CreateFilm(f => f.Trailers = [kept, attached]);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilmForUpdate(film.Id))
            .ReturnsAsync(() => new FilmVersion(
                new Film(film.Name) { Id = film.Id, Trailers = [kept, attached] },
                _fixture.CreateTitle()));

        // Act
        var result = await Update(film, new FilmChangeRequest
        {
            Trailers = [new FilmTrailerChange(kept.Id, null)],
            ExpectedTrailerIds = [kept.Id]
        });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Conflict);
        result.UnexpectedTrailerIds.Should().Equal(attached.Id);
        _saved.Should().BeNull();
        _uploads.Should().BeEmpty();
        _mocker.GetMock<IFilmRepository>().Verify(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "PATCH film trailers is a conflict when a trailer is attached after the edit was checked, and the stale keep-list is not saved.")]
    public async Task trailer_attached_during_a_keep_list_edit_is_conflict()
    {
        // Arrange
        var kept = CreateTrailer();
        var film = CreateFilm(f => f.Trailers = [kept]);
        var attached = CreateTrailer();
        var reads = 0;
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilmForUpdate(film.Id))
            .ReturnsAsync(() =>
            {
                reads++;
                var trailers = reads == 1
                    ? new List<FilmTrailer> { kept }
                    : new List<FilmTrailer> { kept, attached };
                return new FilmVersion(new Film(film.Name) { Id = film.Id, Trailers = trailers }, _fixture.CreateTitle());
            });

        // Act
        var result = await Update(film, Seen(film, new FilmChangeRequest
        {
            Trailers = [new FilmTrailerChange(kept.Id, _fixture.CreateTitle())]
        }));

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Conflict);
        result.UnexpectedTrailerIds.Should().Equal(attached.Id);
        _saved.Should().BeNull();
        _uploads.Should().BeEmpty();
        _mocker.GetMock<IFilmRepository>().Verify(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "PATCH film trailers without the ids the client saw is a conflict and saves nothing, because a keep-list cannot tell a deliberate removal from a client that never listed the stored trailers.")]
    public async Task trailer_keep_list_without_expected_ids_is_conflict()
    {
        // Arrange
        var film = CreateFilm(f => f.Trailers = [CreateTrailer()]);

        // Act
        var result = await Update(film, new FilmChangeRequest
        {
            Trailers = [new FilmTrailerChange(film.Trailers![0].Id, null)]
        });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Conflict);
        result.UnexpectedTrailerIds.Should().Equal(film.Trailers![0].Id);
        _saved.Should().BeNull();
        _uploads.Should().BeEmpty();
    }

    private static FilmChangeRequest Seen(Film film, FilmChangeRequest change)
    {
        change.ExpectedTrailerIds = film.Trailers?.Select(trailer => trailer.Id).ToArray() ?? [];
        return change;
    }

    [Fact(DisplayName =
        "PATCH film on a film deleted after the request read it is not found and saves nothing, so an edit never re-creates a deleted film.")]
    public async Task patch_of_film_deleted_during_the_edit_is_not_found()
    {
        // Arrange
        var film = CreateFilm();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilmForUpdate(film.Id))
            .ReturnsAsync((FilmVersion?)null);

        // Act
        var result = await Update(film, new FilmChangeRequest { TmdbId = 7 });

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.NotFound);
        _saved.Should().BeNull();
        _uploads.Should().BeEmpty();
    }
}
