using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Episodes;
using Api.Services.Films;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using System.Text.Json;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Episodes;

public class EpisodeTrailerAttachServiceRules
{
    private const string ServiceKeysYouTube = RedditPodcastPoster.Models.Podcasts.ServiceKeys.YouTube;

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    // Stored as JSON with a version, like Cosmos: every read is a copy and a replace must match the ETag it read.
    private readonly Dictionary<Guid, (string Json, int Version)> _films = [];
    private int _filmSaves;
    private Action? _beforeReplace;
    private Action? _beforeVerifyRead;
    private int _filmReads;
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private readonly List<Guid> _searchDeletes = [];
    private bool _searchUploadSucceeds = true;
    private bool _searchDeleteSucceeds = true;
    private readonly List<(Guid EpisodeId, string Path)> _redirects = [];
    private Func<WriteResult> _redirectResult = () => new WriteResult(true);
    private readonly List<Film> _createdFilms = [];
    private int _createAttempts;
    // Barriers: hold the first arriving call until the second arrives, so two attaches overlap at that step.
    private TaskCompletionSource? _createGate;
    private int _createArrivals;
    private TaskCompletionSource? _nameLookupGate;
    private int _nameLookupArrivals;
    private Action<Film>? _afterCreate;
    private readonly InMemoryTrailerClaims _claims = new();
    private int _filmDeletes;

    public EpisodeTrailerAttachServiceRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IFilmTrailerClaimStore>(_claims);
        _mocker.Use(TimeProvider.System);
        var films = _mocker.GetMock<IFilmRepository>();
        films
            .Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
            {
                if (++_filmReads > 1)
                {
                    _beforeVerifyRead?.Invoke();
                }

                return Stored(id);
            });
        films
            .Setup(r => r.GetFilmForUpdate(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => Stored(id) is { } film
                ? new FilmVersion(film, _films[id].Version.ToString())
                : null);
        films
            .Setup(r => r.GetFilmIdsByTrailerId(It.IsAny<Guid>()))
            .ReturnsAsync((Guid trailerId) =>
                (IReadOnlyList<Guid>)_films.Keys
                    .Select(Stored)
                    .Where(film => film?.HasTrailer(trailerId) == true)
                    .Select(film => film!.Id)
                    .ToArray());
        films
            .Setup(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()))
            .ReturnsAsync((Film film, string etag) =>
            {
                var hook = _beforeReplace;
                _beforeReplace = null;
                hook?.Invoke();
                if (!_films.TryGetValue(film.Id, out var stored) || stored.Version.ToString() != etag)
                {
                    return false;
                }

                _filmSaves++;
                _films[film.Id] = (JsonSerializer.Serialize(film), stored.Version + 1);
                return true;
            });
        films
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => NameLookup(selector));
        films
            .Setup(r => r.TryCreate(It.IsAny<Film>()))
            .Returns((Film film) => TryCreate(film));
        films
            .Setup(r => r.Delete(It.IsAny<Guid>()))
            .Callback<Guid>(id =>
            {
                _filmDeletes++;
                _films.Remove(id);
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IFileKeyAllocator>()
            .Setup(a => a.Allocate(FileKeyKind.Film, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((FileKeyKind _, string name, Guid _, string? _) => $"allocated-{name.ToLowerInvariant()}");
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        var search = _mocker.GetMock<IPlayableSearchDocumentSwap>();
        search
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploads.Add(documents))
            .ReturnsAsync(() => _searchUploadSucceeds);
        search
            .Setup(s => s.DeleteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => _searchDeletes.AddRange(ids))
            .ReturnsAsync(() => _searchDeleteSucceeds);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Redirect(It.IsAny<PodcastEpisode>(), It.IsAny<string>()))
            .Callback<PodcastEpisode, string>((target, path) => _redirects.Add((target.Episode.Id, path)))
            .ReturnsAsync(() => _redirectResult());
        _mocker.GetMock<ILogger<EpisodeTrailerAttachService>>();
    }

    private Film SeedFilm(Action<Film>? configure = null)
    {
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        configure?.Invoke(film);
        _films[film.Id] = (JsonSerializer.Serialize(film), 1);
        return film;
    }

    /// <summary>Cosmos create: false when a document already has the id. Waits at <see cref="_createGate"/> when set.</summary>
    private async Task<bool> TryCreate(Film film)
    {
        _createAttempts++;
        await Arrive(_createGate, ref _createArrivals);
        if (_films.ContainsKey(film.Id))
        {
            return false;
        }

        _createdFilms.Add(film);
        _films[film.Id] = (JsonSerializer.Serialize(film), 1);
        _afterCreate?.Invoke(film);
        return true;
    }

    /// <summary>The query result is taken on arrival (before any barrier), as a Cosmos query already in flight.</summary>
    private async IAsyncEnumerable<Film> NameLookup(Expression<Func<Film, bool>> selector)
    {
        var matches = _films.Keys.Select(id => Stored(id)!).Where(selector.Compile()).ToArray();
        await Arrive(_nameLookupGate, ref _nameLookupArrivals);
        foreach (var film in matches)
        {
            yield return film;
        }
    }

    private static Task Arrive(TaskCompletionSource? gate, ref int arrivals)
    {
        if (gate is null)
        {
            return Task.CompletedTask;
        }

        if (++arrivals == 2)
        {
            gate.TrySetResult();
        }

        return gate.Task;
    }

    private Film? Stored(Guid id) =>
        _films.TryGetValue(id, out var stored) ? JsonSerializer.Deserialize<Film>(stored.Json) : null;

    /// <summary>Another writer saves the film (unconditionally, as a plain upsert does).</summary>
    private void WriteConcurrently(Guid filmId, Action<Film> change)
    {
        var film = Stored(filmId)!;
        change(film);
        _films[filmId] = (JsonSerializer.Serialize(film), _films[filmId].Version + 1);
    }

    private FilmTrailer CreateTrailer() => new()
    {
        Id = _fixture.CreateGuid(),
        Title = _fixture.CreateTitle(),
        Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
        Length = _fixture.CreateDuration()
    };

    private (RedditPodcastPoster.Models.Podcasts.Podcast Podcast, Episode Episode) SeedEpisode(
        Action<Episode>? configure = null)
    {
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        configure?.Invoke(episode);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        return (podcast, episode);
    }

    private Task<EpisodeTrailerAttachResult> Attach(Guid podcastId, Guid episodeId, Guid filmId, string? title = null) =>
        _mocker.CreateInstance<EpisodeTrailerAttachService>().AttachAsync(
            new EpisodeTrailerAttachCommand(
                podcastId,
                episodeId,
                new EpisodeTrailerAttachRequest { FilmId = filmId, Title = title }),
            CancellationToken.None);

    [Fact(DisplayName =
        "Attaching an episode adds a trailer with the episode id, title, YouTube URL and duration to the film, then deletes only that episode.")]
    public async Task attach_adds_trailer_and_deletes_episode()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        var sibling = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _episodes.Seed(sibling);

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        result.FilmId.Should().Be(film.Id);
        var trailer = Stored(film.Id)!.Trailers.Should().ContainSingle().Subject;
        trailer.Id.Should().Be(episode.Id);
        trailer.Title.Should().Be(episode.Title.Trim());
        trailer.Url.Should().Be(EpisodeServicePresence.TryGetUrl(episode, ServiceKeysYouTube));
        trailer.Length.Should().Be(episode.Length);
        _filmSaves.Should().Be(1);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, sibling.Id)).Should().NotBeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A title in the request names the trailer, so a curator can rename the clip as it is attached.")]
    public async Task attach_uses_request_title()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        var title = _fixture.CreateTitle();

        // Act
        await Attach(podcast.Id, episode.Id, film.Id, $"  {title}  ");

        // Assert
        Stored(film.Id)!.Trailers.Should().ContainSingle().Which.Title.Should().Be(title);
    }

    [Fact(DisplayName =
        "The film search document is pushed with the trailer token and the episode search document is deleted, so search lists the trailer on the film only.")]
    public async Task attach_pushes_film_document_and_deletes_episode_document()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();

        // Act
        await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        var document = _uploads.Should().ContainSingle().Subject.Should().ContainSingle().Subject;
        document.Id.Should().Be(film.Id.ToString());
        document.Trailers.Should().ContainSingle()
            .Which.Should().EndWith($"|{EpisodeServicePresence.TryGetUrl(episode, ServiceKeysYouTube)}|{episode.Title.Trim()}");
        _searchDeletes.Should().Equal(episode.Id);
    }

    [Fact(DisplayName =
        "The episode short link is pointed at the film page, so old trailer links land on the film.")]
    public async Task attach_redirects_short_link_to_film()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();

        // Act
        await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        _redirects.Should().ContainSingle()
            .Which.Should().Be((episode.Id, CatalogueShortId.PlayablePath(film.Name, film.Id, CatalogueShortId.Film)));
    }

    [Fact(DisplayName =
        "A failed short-link redirect does not fail the attach, because the film and search are already correct.")]
    public async Task redirect_failure_still_accepts()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _redirectResult = () => throw new InvalidOperationException(_fixture.CreateTitle());

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "A failed film search push keeps the episode row and short link and reports an indexing failure, so a re-send can resume.")]
    public async Task search_push_failure_keeps_episode()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _searchUploadSucceeds = false;

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _searchDeletes.Should().BeEmpty();
        _redirects.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A failed episode search delete keeps the episode row, so search never points at a deleted episode.")]
    public async Task search_delete_failure_keeps_episode()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _searchDeleteSucceeds = false;

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _redirects.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Attaching an episode that is already a trailer of another film is a Conflict naming that film, the second film is left unchanged, and the episode row stays, because one episode cannot be a trailer of two films.")]
    public async Task episode_already_a_trailer_of_another_film_is_rejected()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var existing = CreateTrailer();
        existing.Id = episode.Id;
        var holding = SeedFilm(film => film.Trailers = [existing]);
        var target = SeedFilm();

        // Act
        var result = await Attach(podcast.Id, episode.Id, target.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Conflict);
        result.FilmId.Should().Be(holding.Id);
        result.Error.Should().Be("This episode is already a trailer of another film");
        Stored(target.Id)!.Trailers.Should().BeNull();
        Stored(holding.Id)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        _filmSaves.Should().Be(0);
        _uploads.Should().BeEmpty();
        _searchDeletes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the lookup of films already holding the trailer id fails, the attach fails and the episode stays, so a trailer is not added while that check is unknown.")]
    public async Task trailer_lookup_failure_keeps_episode()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilmIdsByTrailerId(episode.Id))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeFalse();
        _filmSaves.Should().Be(0);
        _uploads.Should().BeEmpty();
        Stored(film.Id)!.Trailers.Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "The film being attached to is not treated as another holder of the trailer, so a re-read that already lists this film still adds the trailer.")]
    public async Task destination_film_in_the_trailer_lookup_does_not_block_the_attach()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilmIdsByTrailerId(episode.Id))
            .ReturnsAsync([film.Id]);

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        Stored(film.Id)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Re-sending after an interrupted attach does not add the trailer twice and finishes deleting the episode.")]
    public async Task resend_after_interrupted_attach_resumes()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _searchUploadSucceeds = false;
        await Attach(podcast.Id, episode.Id, film.Id);
        _searchUploadSucceeds = true;
        _filmReads = 0;

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        Stored(film.Id)!.Trailers.Should().ContainSingle();
        _filmSaves.Should().Be(1);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Re-sending after a completed attach is accepted for the same film, so a double click or lost 202 is harmless.")]
    public async Task resend_after_completed_attach_is_accepted()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        await Attach(podcast.Id, episode.Id, film.Id);

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        result.FilmId.Should().Be(film.Id);
        Stored(film.Id)!.Trailers.Should().ContainSingle();
        _uploads.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A missing film, episode or podcast is not found and nothing is written.")]
    public async Task missing_targets_are_not_found()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();

        // Act
        var noFilm = await Attach(podcast.Id, episode.Id, _fixture.CreateGuid());
        var noEpisode = await Attach(podcast.Id, _fixture.CreateGuid(), film.Id);
        var noPodcast = await Attach(_fixture.CreateGuid(), episode.Id, film.Id);

        // Assert
        noFilm.Status.Should().Be(EpisodeTrailerAttachStatus.NotFound);
        noEpisode.Status.Should().Be(EpisodeTrailerAttachStatus.NotFound);
        noPodcast.Status.Should().Be(EpisodeTrailerAttachStatus.NotFound);
        _filmSaves.Should().Be(0);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A removed episode, a removed film, a missing film id, or an episode with no link is a bad request and nothing is written.")]
    public async Task invalid_targets_are_bad_requests()
    {
        // Arrange
        var film = SeedFilm();
        var removedFilm = SeedFilm(f => f.Removed = true);
        var (podcast, episode) = SeedEpisode();
        var (removedPodcast, removedEpisode) = SeedEpisode(e => e.Removed = true);
        var (linklessPodcast, linkless) = SeedEpisode(e => e.Services = null);

        // Act
        var results = new[]
        {
            await Attach(removedPodcast.Id, removedEpisode.Id, film.Id),
            await Attach(podcast.Id, episode.Id, removedFilm.Id),
            await Attach(podcast.Id, episode.Id, Guid.Empty),
            await Attach(linklessPodcast.Id, linkless.Id, film.Id)
        };

        // Assert
        results.Should().OnlyContain(r => r.Status == EpisodeTrailerAttachStatus.BadRequest);
        _filmSaves.Should().Be(0);
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When another attach saves the film between this attach's read and save, both trailers are kept, because the save is ETag-guarded and re-reads on a conflict.")]
    public async Task concurrent_attach_keeps_both_trailers()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        var other = CreateTrailer();
        _beforeReplace = () => WriteConcurrently(film.Id, f => f.Trailers = [other]);

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        Stored(film.Id)!.Trailers!.Select(t => t.Id).Should().Equal(other.Id, episode.Id);
        _uploads.Should().ContainSingle().Which.Single().Trailers.Should().HaveCount(2);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "When a stale film write drops the new trailer before the episode is deleted, the attach fails as an indexing failure and keeps the episode, and a re-send finishes it.")]
    public async Task dropped_trailer_keeps_episode_and_resend_finishes()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _beforeVerifyRead = () =>
        {
            _beforeVerifyRead = null;
            WriteConcurrently(film.Id, f => f.Trailers = null);
        };

        // Act
        var first = await Attach(podcast.Id, episode.Id, film.Id);
        var episodeAfterFirst = await _episodes.GetEpisode(podcast.Id, episode.Id);
        _filmReads = 0;
        var resent = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        first.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        first.FailureIndexing.Should().BeTrue();
        episodeAfterFirst.Should().NotBeNull();
        resent.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        Stored(film.Id)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "When the film keeps changing on every save attempt, the attach fails without touching search or the episode.")]
    public async Task persistent_conflict_fails_and_keeps_episode()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        _uploads.Should().BeEmpty();
        _searchDeletes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        Stored(film.Id)!.Trailers.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the film is deleted after the trailer is saved, the episode search document is put back and the episode row is kept, and a re-send reports the film as not found.")]
    public async Task film_deleted_after_save_restores_episode_search_document()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _beforeVerifyRead = () =>
        {
            _beforeVerifyRead = null;
            _films.Remove(film.Id);
        };

        // Act
        var first = await Attach(podcast.Id, episode.Id, film.Id);
        var episodeAfterFirst = await _episodes.GetEpisode(podcast.Id, episode.Id);
        var resent = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        first.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        first.FailureIndexing.Should().BeTrue();
        episodeAfterFirst.Should().NotBeNull();
        resent.Status.Should().Be(EpisodeTrailerAttachStatus.NotFound);
        resent.Error.Should().Be("Film not found");
        _searchDeletes.Should().Equal(episode.Id);
        _redirects.Should().BeEmpty();
        _uploads.Should().HaveCount(2);
        _uploads[1].Should().ContainSingle().Which.Should().BeEquivalentTo(
            new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord());
        LoggedFilmGone(LogLevel.Warning, episode.Id, film.Id);
    }

    [Fact(DisplayName =
        "When the film is deleted after the trailer is saved and putting the episode search document back fails, the episode row is still kept.")]
    public async Task film_deleted_after_save_keeps_episode_when_search_restore_fails()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        _beforeVerifyRead = () =>
        {
            _beforeVerifyRead = null;
            _films.Remove(film.Id);
            _searchUploadSucceeds = false;
        };

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _uploads.Should().HaveCount(2);
        _uploads[1].Should().ContainSingle().Which.Id.Should().Be(episode.Id.ToString());
        LoggedFilmGone(LogLevel.Warning, episode.Id, film.Id);
        LoggedFilmGone(LogLevel.Error, episode.Id, film.Id);
    }

    private Task<EpisodeTrailerAttachResult> AttachCreating(
        Guid podcastId,
        Guid episodeId,
        string filmName,
        string? identityDecision = null,
        string? selectedIdentityKey = null) =>
        _mocker.CreateInstance<EpisodeTrailerAttachService>().AttachAsync(
            new EpisodeTrailerAttachCommand(
                podcastId,
                episodeId,
                new EpisodeTrailerAttachRequest
                {
                    CreateFilm = true,
                    FilmName = filmName,
                    IdentityDecision = identityDecision,
                    SelectedIdentityKey = selectedIdentityKey
                }),
            CancellationToken.None);

    [Fact(DisplayName =
        "#1418: createFilm with a name no film has creates an unreleased film (no release, no services, allocated file key), attaches the trailer to it and deletes the episode.")]
    public async Task create_film_creates_unreleased_film_and_attaches_trailer()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var filmName = _fixture.CreateTitle();

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, $"  {filmName}  ");

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        result.FilmCreated.Should().BeTrue();
        var created = _createdFilms.Should().ContainSingle().Subject;
        result.FilmId.Should().Be(created.Id);
        result.Name.Should().Be(filmName);
        var stored = Stored(created.Id)!;
        stored.Name.Should().Be(filmName);
        stored.Release.Should().BeNull();
        stored.Services.Should().BeNullOrEmpty();
        stored.FileKey.Should().Be($"allocated-{filmName.ToLowerInvariant()}");
        stored.Id.Should().Be(EpisodeTrailerAttachService.NewFilmId(filmName));
        stored.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        _uploads.Should().ContainSingle().Which.Should().ContainSingle().Which.Id.Should().Be(created.Id.ToString());
    }

    [Fact(DisplayName =
        "#1418: createFilm with a name one film already has (any case) attaches the trailer to that film and creates nothing, like createShow joins an existing show.")]
    public async Task create_film_reuses_film_with_same_name()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, film.Name.ToUpperInvariant());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        result.FilmId.Should().Be(film.Id);
        result.FilmCreated.Should().BeFalse();
        _createdFilms.Should().BeEmpty();
        Stored(film.Id)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        _mocker.GetMock<ICatalogueIdentityEnricher>().Verify(
            e => e.ResolveFilmAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CatalogueIdentityChoice>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "#1418: createFilm when two films already have the name returns 409 and writes nothing.")]
    public async Task create_film_with_two_films_of_that_name_is_conflict()
    {
        // Arrange
        var first = SeedFilm();
        SeedFilm(film => film.Name = first.Name);
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, first.Name);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Conflict);
        _createdFilms.Should().BeEmpty();
        _filmSaves.Should().Be(0);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "#1418: createFilm with an ambiguous IMDb/TMDB match returns the candidates and writes nothing: no film, no trailer, episode kept.")]
    public async Task create_film_ambiguous_identity_returns_prompt_and_writes_nothing()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var filmName = _fixture.CreateTitle();
        var hold = new CatalogueIdentityHold(
            CatalogueAuthorityCandidate.FilmKind,
            filmName,
            [
                new CatalogueAuthorityCandidate(CatalogueAuthorityCandidate.TmdbKey(101), filmName, 2026,
                    CatalogueAuthorityCandidate.FilmKind, CatalogueAuthorityCandidate.TmdbSource, 101, null),
                new CatalogueAuthorityCandidate(CatalogueAuthorityCandidate.TmdbKey(202), filmName, 1999,
                    CatalogueAuthorityCandidate.FilmKind, CatalogueAuthorityCandidate.TmdbSource, 202, null)
            ]);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                filmName, null, CatalogueIdentityChoice.Unset, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.NeedsPrompt(hold));

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, filmName);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.IdentityDecisionRequired);
        result.IdentityHold.Should().BeSameAs(hold);
        _createdFilms.Should().BeEmpty();
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "#1418: createFilm re-sent with identityDecision select and a TMDB key fills the new film's ids from the chosen candidate.")]
    public async Task create_film_select_fills_identity()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var filmName = _fixture.CreateTitle();
        var key = CatalogueAuthorityCandidate.TmdbKey(303);
        var ids = new CatalogueAuthorityIds("tt0000303", 303, null);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                filmName, null, CatalogueIdentityChoice.Select, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed(ids));
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ApplyMissing(It.IsAny<Film>(), ids))
            .Callback<Film, CatalogueAuthorityIds?>((film, _) => ids.FillMissing(film))
            .Returns(true);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, filmName, "select", key);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        var stored = Stored(result.FilmId!.Value)!;
        stored.TmdbId.Should().Be(303);
        stored.ImdbId.Should().Be("tt0000303");
    }

    [Fact(DisplayName =
        "#1418: createFilm with identityDecision cancel returns Cancelled and writes nothing.")]
    public async Task create_film_cancel_writes_nothing()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle(), "cancel");

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Cancelled);
        _createdFilms.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "#1418: createFilm for a removed episode returns 400 before any film is created or identity looked up.")]
    public async Task create_film_removed_episode_creates_nothing()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode(e => e.Removed = true);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.BadRequest);
        _createdFilms.Should().BeEmpty();
        _mocker.GetMock<ICatalogueIdentityEnricher>().Verify(
            e => e.ResolveFilmAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CatalogueIdentityChoice>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "#1418: createFilm for an episode with no playable link returns 400 before any film is created.")]
    public async Task create_film_episode_without_link_creates_nothing()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode(e => e.Services = null);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.BadRequest);
        _createdFilms.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "#1418: createFilm re-sent after a failed search push reuses the film created by the first call, so the film is never created twice.")]
    public async Task create_film_resend_after_failure_reuses_created_film()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var filmName = _fixture.CreateTitle();
        _searchUploadSucceeds = false;
        var first = await AttachCreating(podcast.Id, episode.Id, filmName);
        _searchUploadSucceeds = true;

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, filmName);

        // Assert
        first.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        first.FilmId.Should().Be(result.FilmId);
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        result.FilmCreated.Should().BeFalse();
        _createdFilms.Should().ContainSingle().Which.Id.Should().Be(result.FilmId!.Value);
        Stored(result.FilmId!.Value)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "#1418: the new film is written once, by a conditional create that already holds the trailer, so no empty film is ever stored.")]
    public async Task create_film_single_conditional_write_holds_trailer()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        Film? atCreate = null;
        _afterCreate = film => atCreate = Stored(film.Id);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        atCreate!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        _createAttempts.Should().Be(1);
        _filmSaves.Should().Be(0);
        _mocker.GetMock<IFilmRepository>().Verify(r => r.Save(It.IsAny<Film>()), Times.Never);
    }

    [Fact(DisplayName =
        "#1418: when the search push fails after the create, the new film still holds the trailer and the episode row is kept, so nothing is orphaned and a re-send resumes.")]
    public async Task create_film_failure_after_create_leaves_film_with_trailer()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        _searchUploadSucceeds = false;

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        Stored(result.FilmId!.Value)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "#1418: two overlapping createFilm calls for the same name that both reach the create converge on one film with one trailer, and both are accepted.")]
    public async Task create_film_overlapping_creates_converge_on_one_film()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var filmName = _fixture.CreateTitle();
        _createGate = new TaskCompletionSource();

        // Act
        var results = await Task.WhenAll(
            AttachCreating(podcast.Id, episode.Id, filmName),
            AttachCreating(podcast.Id, episode.Id, filmName.ToUpperInvariant()));

        // Assert
        _createAttempts.Should().Be(2);
        results.Should().OnlyContain(r => r.Status == EpisodeTrailerAttachStatus.Accepted);
        results.Select(r => r.FilmId).Distinct().Should().ContainSingle();
        results.Count(r => r.FilmCreated).Should().Be(1);
        var film = _films.Keys.Select(Stored).Should().ContainSingle().Subject!;
        film.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "#1418: when another call creates the film between this call's name query and its name check, this call attaches to that film instead of returning 409.")]
    public async Task create_film_overlapping_name_checks_converge_on_one_film()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var filmName = _fixture.CreateTitle();
        _nameLookupGate = new TaskCompletionSource();

        // Act
        var results = await Task.WhenAll(
            AttachCreating(podcast.Id, episode.Id, filmName),
            AttachCreating(podcast.Id, episode.Id, filmName));

        // Assert
        results.Should().OnlyContain(r => r.Status == EpisodeTrailerAttachStatus.Accepted);
        results.Select(r => r.FilmId).Distinct().Should().ContainSingle();
        _createdFilms.Should().ContainSingle();
        _films.Keys.Select(Stored).Should().ContainSingle()
            .Which!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "#1418: two overlapping createFilm calls with different names for one episode, both past every check, are decided by the one conditional claim: exactly one film is created and keeps the trailer, the other call gets 409 naming it, writes nothing and deletes nothing.")]
    public async Task create_film_different_names_overlapping_one_owner()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        _claims.Gate = new TaskCompletionSource();

        // Act
        var results = await Task.WhenAll(
            AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle()),
            AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle()));

        // Assert
        var winner = results.Should().ContainSingle(r => r.Status == EpisodeTrailerAttachStatus.Accepted).Subject;
        var loser = results.Should().ContainSingle(r => r.Status == EpisodeTrailerAttachStatus.Conflict).Subject;
        loser.FilmId.Should().Be(winner.FilmId);
        _createdFilms.Should().ContainSingle().Which.Id.Should().Be(winner.FilmId!.Value);
        _films.Keys.Should().Equal(winner.FilmId.Value);
        Stored(winner.FilmId.Value)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        _filmDeletes.Should().Be(0);
        _claims.Owner(episode.Id).Should().Be(winner.FilmId.Value);
    }

    [Fact(DisplayName =
        "#1418: a second trailer added to the new film by another request mid-attach is kept: the attach completes and no film is deleted.")]
    public async Task create_film_concurrent_second_trailer_is_kept()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var second = CreateTrailer();
        _afterCreate = film => WriteConcurrently(film.Id, f => f.Trailers = [.. f.Trailers!, second]);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        Stored(result.FilmId!.Value)!.Trailers!.Select(t => t.Id).Should().BeEquivalentTo([episode.Id, second.Id]);
        _filmDeletes.Should().Be(0);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "#1418: a main video added to the new film by another request mid-attach is kept: the attach completes and no film is deleted.")]
    public async Task create_film_concurrent_main_video_is_kept()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var video = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");
        _afterCreate = film => WriteConcurrently(film.Id, f => f.Services = new Dictionary<string, ServiceLink>
        {
            [ServiceKeysYouTube] = new() { Url = video }
        });

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        var stored = Stored(result.FilmId!.Value)!;
        stored.Services![ServiceKeysYouTube].Url.Should().Be(video);
        stored.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        _filmDeletes.Should().Be(0);
    }

    [Fact(DisplayName =
        "#1418: a different-name createFilm for an episode whose film has meanwhile gained a second trailer and a main video gets 409 naming that film, which is left exactly as it was; nothing is created or deleted.")]
    public async Task create_film_other_name_never_touches_film_with_foreign_content()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        _searchUploadSucceeds = false;
        var first = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());
        var filmId = first.FilmId!.Value;
        WriteConcurrently(filmId, f =>
        {
            f.Trailers = [.. f.Trailers!, CreateTrailer()];
            f.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeysYouTube] = new() { Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}") }
            };
        });
        var before = _films[filmId];
        _searchUploadSucceeds = true;

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        first.Status.Should().Be(EpisodeTrailerAttachStatus.Failed);
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Conflict);
        result.FilmId.Should().Be(filmId);
        _films.Should().ContainSingle();
        _films[filmId].Should().Be(before);
        _createdFilms.Should().ContainSingle();
        _filmDeletes.Should().Be(0);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "#1418: while another attach's fresh claim holds the episode (its film not written yet), createFilm returns 409 'in progress' naming that film and writes nothing.")]
    public async Task create_film_fresh_foreign_claim_is_conflict_without_writes()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var otherFilmId = _fixture.CreateGuid();
        _claims.Seed(episode.Id, otherFilmId, DateTime.UtcNow);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Conflict);
        result.FilmId.Should().Be(otherFilmId);
        result.Error.Should().Contain("in progress");
        _createAttempts.Should().Be(0);
        _films.Should().BeEmpty();
        _claims.Owner(episode.Id).Should().Be(otherFilmId);
        _mocker.GetMock<IFileKeyAllocator>().Verify(
            a => a.Allocate(It.IsAny<FileKeyKind>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "#1418: a claim older than the stale window whose film never took the trailer (a failed attach) is taken over, and the attach completes.")]
    public async Task create_film_stale_claim_is_taken_over()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        _claims.Seed(
            episode.Id,
            _fixture.CreateGuid(),
            DateTime.UtcNow - EpisodeTrailerAttachService.ClaimStaleAfter - TimeSpan.FromMinutes(1));

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        _claims.Owner(episode.Id).Should().Be(result.FilmId!.Value);
        Stored(result.FilmId.Value)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "#1418: attaching by filmId while another film's fresh claim holds the episode returns 409 naming that film and leaves the target film unchanged.")]
    public async Task attach_by_id_foreign_claim_is_conflict_without_writes()
    {
        // Arrange
        var film = SeedFilm();
        var (podcast, episode) = SeedEpisode();
        var otherFilmId = _fixture.CreateGuid();
        _claims.Seed(episode.Id, otherFilmId, DateTime.UtcNow);
        var before = _films[film.Id];

        // Act
        var result = await Attach(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Conflict);
        result.FilmId.Should().Be(otherFilmId);
        _films[film.Id].Should().Be(before);
        _filmSaves.Should().Be(0);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }
    [Fact(DisplayName =
        "#1418: when the name's film id belongs to a film renamed since, the call returns 409 naming it and does not attach to it.")]
    public async Task create_film_id_held_by_renamed_film_is_conflict()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var filmName = _fixture.CreateTitle();
        var renamed = SeedFilm(film => film.Id = EpisodeTrailerAttachService.NewFilmId(filmName));

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, filmName);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Conflict);
        result.FilmId.Should().Be(renamed.Id);
        Stored(renamed.Id)!.Trailers.Should().BeNullOrEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "#1418: the film id for a name ignores case and surrounding spaces, and differs between names.")]
    public void new_film_id_is_derived_from_normalised_name()
    {
        // Arrange
        var name = _fixture.CreateTitle();

        // Act
        var id = EpisodeTrailerAttachService.NewFilmId(name);

        // Assert
        EpisodeTrailerAttachService.NewFilmId($"  {name.ToUpperInvariant()} ").Should().Be(id);
        EpisodeTrailerAttachService.NewFilmId(name + " 2").Should().NotBe(id);
        id.ToString()[14].Should().Be('5');
    }

    [Fact(DisplayName =
        "#1418: createFilm re-sent after a completed attach (episode row gone) is accepted for the film holding the trailer.")]
    public async Task create_film_resend_after_completed_attach_is_accepted()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var filmName = _fixture.CreateTitle();
        var first = await AttachCreating(podcast.Id, episode.Id, filmName);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, filmName);

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Accepted);
        result.FilmId.Should().Be(first.FilmId);
        _createdFilms.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "#1418: createFilm for an episode that is already a trailer of a film with another name returns 409 naming that film.")]
    public async Task create_film_episode_held_by_other_film_is_conflict()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var holder = SeedFilm(film => film.Trailers = [FilmTrailerFactory.From(episode, null)!]);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTrailerAttachStatus.Conflict);
        result.FilmId.Should().Be(holder.Id);
        _createdFilms.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "#1418: createFilm with a blank filmName returns 400; giving both filmId and createFilm returns 400.")]
    public async Task create_film_request_validation()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var sut = _mocker.CreateInstance<EpisodeTrailerAttachService>();

        // Act
        var blank = await AttachCreating(podcast.Id, episode.Id, "  ");
        var both = await sut.AttachAsync(
            new EpisodeTrailerAttachCommand(
                podcast.Id,
                episode.Id,
                new EpisodeTrailerAttachRequest
                {
                    FilmId = _fixture.CreateGuid(),
                    CreateFilm = true,
                    FilmName = _fixture.CreateTitle()
                }),
            CancellationToken.None);

        // Assert
        blank.Status.Should().Be(EpisodeTrailerAttachStatus.BadRequest);
        both.Status.Should().Be(EpisodeTrailerAttachStatus.BadRequest);
        _createdFilms.Should().BeEmpty();
    }

    private void LoggedFilmGone(LogLevel level, Guid episodeId, Guid filmId)
    {
        var fragments = new[] { "TrailerAttachFilmGone", episodeId.ToString(), filmId.ToString() };
        _mocker.GetMock<ILogger<EpisodeTrailerAttachService>>().Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => fragments.All(fragment => state!.ToString()!.Contains(fragment))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// LookUps claims like Cosmos: a create fails when the id exists, a takeover must match the version it read.
    /// <see cref="Gate"/> holds the first claimer until the second arrives, so both have passed every check.
    /// </summary>
    private sealed class InMemoryTrailerClaims : IFilmTrailerClaimStore
    {
        private readonly Dictionary<Guid, (FilmTrailerClaim Claim, int Version)> _claims = [];
        private int _arrivals;

        public TaskCompletionSource? Gate { get; set; }

        public void Seed(Guid episodeId, Guid filmId, DateTime claimedUtc) =>
            _claims[episodeId] = (new FilmTrailerClaim { EpisodeId = episodeId, FilmId = filmId, ClaimedUtc = claimedUtc }, 1);

        public Guid? Owner(Guid episodeId) => _claims.TryGetValue(episodeId, out var held) ? held.Claim.FilmId : null;

        public async Task<FilmTrailerClaimAttempt> TryClaim(Guid episodeId, Guid filmId, DateTime nowUtc)
        {
            await Arrive(Gate, ref _arrivals);
            if (!_claims.TryGetValue(episodeId, out var held))
            {
                Seed(episodeId, filmId, nowUtc);
                return new FilmTrailerClaimAttempt(true);
            }

            return held.Claim.FilmId == filmId
                ? new FilmTrailerClaimAttempt(true)
                : new FilmTrailerClaimAttempt(false, held.Claim, held.Version.ToString());
        }

        public Task<bool> TryTakeOver(Guid episodeId, Guid filmId, DateTime nowUtc, string holderETag)
        {
            if (!_claims.TryGetValue(episodeId, out var held) || held.Version.ToString() != holderETag)
            {
                return Task.FromResult(false);
            }

            _claims[episodeId] = (new FilmTrailerClaim { EpisodeId = episodeId, FilmId = filmId, ClaimedUtc = nowUtc },
                held.Version + 1);
            return Task.FromResult(true);
        }
    }
}