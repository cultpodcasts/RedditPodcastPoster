using System.Linq.Expressions;
using System.Text.Json;
using Api.Models;
using Api.Services.Episodes;
using Api.Services.Films;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Tmdb.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Films;

/// <summary>
/// RPP #1413: <c>POST film</c> creates an empty film from a TMDB / IMDb identity without moving an episode
/// (docs/create-film-from-identity.md).
/// </summary>
public class FilmCreateServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    // Stored as JSON, like Cosmos: every read is a copy.
    private readonly Dictionary<Guid, string> _films = [];
    private readonly List<Film> _created = [];
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private bool _searchUploadSucceeds = true;
    private bool _createSucceeds = true;
    private TmdbMovieMetadata? _metadata;

    public FilmCreateServiceRules()
    {
        var films = _mocker.GetMock<IFilmRepository>();
        films
            .Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => Stored(id));
        films
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => Query(selector));
        films
            .Setup(r => r.TryCreate(It.IsAny<Film>()))
            .ReturnsAsync((Film film) =>
            {
                if (!_createSucceeds || _films.ContainsKey(film.Id))
                {
                    return false;
                }

                _created.Add(film);
                _films[film.Id] = JsonSerializer.Serialize(film);
                return true;
            });
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchFilmAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _metadata);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        _mocker.GetMock<IFileKeyAllocator>()
            .Setup(a => a.Allocate(FileKeyKind.Film, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((FileKeyKind _, string name, Guid _, string? _) => $"allocated-{name.ToLowerInvariant()}");
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploads.Add(documents))
            .ReturnsAsync(() => _searchUploadSucceeds);
    }

    private Film? Stored(Guid id) =>
        _films.TryGetValue(id, out var json) ? JsonSerializer.Deserialize<Film>(json) : null;

    private async IAsyncEnumerable<Film> Query(Expression<Func<Film, bool>> selector)
    {
        var matches = _films.Keys.Select(id => Stored(id)!).Where(selector.Compile()).ToArray();
        await Task.Yield();
        foreach (var film in matches)
        {
            yield return film;
        }
    }

    private Film SeedFilm(Action<Film>? configure = null)
    {
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        configure?.Invoke(film);
        _films[film.Id] = JsonSerializer.Serialize(film);
        return film;
    }

    private TmdbMovieMetadata ArrangeIdentity(
        int tmdbId,
        DateOnly? releaseDate = null,
        int? releaseYear = null,
        string? publisher = null)
    {
        _metadata = new TmdbMovieMetadata(
            tmdbId,
            _fixture.CreateTitle(),
            null,
            _fixture.CreateTitle(12),
            (int)_fixture.CreateDuration().TotalMinutes,
            releaseDate,
            null,
            [],
            publisher,
            releaseYear);
        return _metadata;
    }

    private Task<FilmCreateResult> Create(FilmCreateRequest request) =>
        _mocker.CreateInstance<FilmCreateService>().CreateAsync(new FilmCreateCommand(request), CancellationToken.None);

    [Fact(DisplayName =
        "Creating a film by TMDB id names it from the identity title, stores the TMDB id, and keeps it empty: no services, trailers or episode, because the film starts with no playable link.")]
    public async Task create_by_tmdb_id_makes_empty_film_named_from_identity()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var identity = ArrangeIdentity(tmdbId, DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(30)));

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Accepted);
        var film = Stored(result.FilmId!.Value)!;
        result.Name.Should().Be(identity.Title);
        film.Name.Should().Be(identity.Title);
        film.TmdbId.Should().Be(tmdbId);
        film.Description.Should().Be(identity.Overview);
        film.Length.Should().Be(TimeSpan.FromMinutes(identity.RuntimeMinutes!.Value));
        film.Services.Should().BeNullOrEmpty();
        film.Trailers.Should().BeNullOrEmpty();
        film.Removed.Should().NotBe(true);
        _created.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Creating a film by IMDb id looks the movie up by that id, stores the IMDb id and the TMDB id the lookup returns, because a curator may know only one of them.")]
    public async Task create_by_imdb_id_stores_both_ids()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var tmdbId = _fixture.CreateTmdbId();
        ArrangeIdentity(tmdbId);

        // Act
        var result = await Create(new FilmCreateRequest { ImdbId = $"  {imdbId.ToUpperInvariant()}  " });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Accepted);
        var film = Stored(result.FilmId!.Value)!;
        film.ImdbId.Should().Be(imdbId);
        film.TmdbId.Should().Be(tmdbId);
        _mocker.GetMock<IIdentityMetadataEnricher>().Verify(
            e => e.FetchFilmAsync(null, imdbId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "The release comes from the identity: a full TMDB release date becomes a date release, because the film is released on that day.")]
    public async Task release_from_identity_date()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var date = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(90));
        ArrangeIdentity(tmdbId, date);

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId });

        // Assert
        var release = Stored(result.FilmId!.Value)!.Release;
        release.Should().NotBeNull();
        release!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        release.Date.Should().Be(date);
    }

    [Fact(DisplayName =
        "The release comes from the identity: when TMDB only knows the year, the film's release is that year, because nothing more precise is known.")]
    public async Task release_from_identity_year_only()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var year = DomainTestFixture.UtcDateDaysAgo(400).Year;
        ArrangeIdentity(tmdbId, releaseYear: year);

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId });

        // Assert
        var release = Stored(result.FilmId!.Value)!.Release;
        release.Should().NotBeNull();
        release!.Precision.Should().Be(CatalogueReleasePrecision.Year);
        release.Year.Should().Be(year);
    }

    [Fact(DisplayName =
        "A title in the request names the film instead of the identity title, and a publisher in the request wins over the identity's, because the curator's words are kept.")]
    public async Task request_title_and_publisher_override_identity()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var identity = ArrangeIdentity(tmdbId, publisher: _fixture.CreateTitle());
        var title = _fixture.CreateTitleNotContainedIn(identity.Title);
        var publisher = _fixture.CreateTitleNotContainedIn(identity.Publisher!);

        // Act
        var result = await Create(new FilmCreateRequest
        {
            TmdbId = tmdbId,
            Title = $"  {title}  ",
            Publisher = publisher
        });

        // Assert
        var film = Stored(result.FilmId!.Value)!;
        film.Name.Should().Be(title);
        film.PublisherName.Should().Be(publisher);
    }

    [Fact(DisplayName =
        "The new film is pushed to search with the film content kind and its ids, even with no main video, because a later trailer attach and the film page both need the document.")]
    public async Task new_film_is_indexed_without_a_main_video()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        ArrangeIdentity(tmdbId);

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId });

        // Assert
        var document = _uploads.Should().ContainSingle().Subject.Should().ContainSingle().Subject;
        document.Id.Should().Be(result.FilmId!.Value.ToString());
        document.TmdbId.Should().Be(tmdbId);
        document.ContentKind.Should().Be(SearchContentKind.Film);
    }

    [Fact(DisplayName =
        "When a film already has the same TMDB id, the create is a conflict naming that film and writes nothing, because the identity must not be duplicated.")]
    public async Task tmdb_identity_clash_is_conflict()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var existing = SeedFilm(f => f.TmdbId = tmdbId);
        ArrangeIdentity(tmdbId);

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Conflict);
        result.FilmId.Should().Be(existing.Id);
        _created.Should().BeEmpty();
        _uploads.Should().BeEmpty();
        _mocker.GetMock<IIdentityMetadataEnricher>().Verify(
            e => e.FetchFilmAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a film already has the same IMDb id, the create is a conflict naming that film and writes nothing, so re-sending the same identity is safe and returns the existing film's id.")]
    public async Task imdb_identity_clash_is_conflict()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var existing = SeedFilm(f => f.ImdbId = imdbId);
        ArrangeIdentity(_fixture.CreateTmdbId());

        // Act
        var result = await Create(new FilmCreateRequest { ImdbId = imdbId });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Conflict);
        result.FilmId.Should().Be(existing.Id);
        _created.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When only an IMDb id is sent and the TMDB id it resolves to already belongs to a film, the create is a conflict naming that film, because the same movie is not made twice.")]
    public async Task resolved_tmdb_identity_clash_is_conflict()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var existing = SeedFilm(f => f.TmdbId = tmdbId);
        ArrangeIdentity(tmdbId);

        // Act
        var result = await Create(new FilmCreateRequest { ImdbId = _fixture.CreateImdbId() });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Conflict);
        result.FilmId.Should().Be(existing.Id);
        _created.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a film already has the name (case-insensitive), the create is a 409 conflict naming that film and writes nothing, because film names are unique.")]
    public async Task name_clash_is_conflict()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var identity = ArrangeIdentity(tmdbId);
        var existing = SeedFilm(f => f.Name = identity.Title.ToUpperInvariant());

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Conflict);
        result.FilmId.Should().Be(existing.Id);
        _created.Should().BeEmpty();
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When another request creates the same-named film between the checks and the write, the create is a conflict naming the winner and does not push search, because creates converge on one document.")]
    public async Task lost_create_race_is_conflict_naming_winner()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var identity = ArrangeIdentity(tmdbId);
        var winnerId = EpisodeTrailerAttachService.NewFilmId(identity.Title);
        _films[winnerId] = JsonSerializer.Serialize(new Film(identity.Title) { Id = winnerId });
        // The name query ran before the winner's write: make this create see only the failed TryCreate.
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns(Query(_ => false));

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Conflict);
        result.FilmId.Should().Be(winnerId);
        _created.Should().BeEmpty();
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When TMDB has no movie for the id, the create is IdentityNotFound and writes nothing, because an unverified identity must not be stored.")]
    public async Task unknown_identity_is_not_found()
    {
        // Arrange
        _metadata = null;

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = _fixture.CreateTmdbId() });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.IdentityNotFound);
        _created.Should().BeEmpty();
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A title with no ids and an ambiguous identity returns the candidates for the curator and writes nothing, as Move to film does.")]
    public async Task ambiguous_title_returns_identity_decision()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var hold = new CatalogueIdentityHold(
            CatalogueAuthorityCandidate.FilmKind,
            title,
            [
                new CatalogueAuthorityCandidate(
                    CatalogueAuthorityCandidate.TmdbKey(_fixture.CreateTmdbId()), title, null,
                    CatalogueAuthorityCandidate.FilmKind, CatalogueAuthorityCandidate.TmdbSource, null, null),
                new CatalogueAuthorityCandidate(
                    CatalogueAuthorityCandidate.TmdbKey(_fixture.CreateTmdbId()), title, null,
                    CatalogueAuthorityCandidate.FilmKind, CatalogueAuthorityCandidate.TmdbSource, null, null)
            ]);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                title, null, CatalogueIdentityChoice.Unset, null, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(CatalogueIdentityAdmission.NeedsPrompt(hold));

        // Act
        var result = await Create(new FilmCreateRequest { Title = title });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.IdentityDecisionRequired);
        result.IdentityHold.Should().BeSameAs(hold);
        _created.Should().BeEmpty();
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Re-sending a title with select and a TMDB candidate key creates the film with the chosen candidate's ids and release from the identity.")]
    public async Task select_creates_film_with_chosen_identity()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var tmdbId = _fixture.CreateTmdbId();
        var imdbId = _fixture.CreateImdbId();
        var key = CatalogueAuthorityCandidate.TmdbKey(tmdbId);
        var ids = new CatalogueAuthorityIds(imdbId, tmdbId, null);
        var date = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(60));
        ArrangeIdentity(tmdbId, date);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                title, null, CatalogueIdentityChoice.Select, key, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed(ids));
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ApplyMissing(It.IsAny<Film>(), ids))
            .Callback<Film, CatalogueAuthorityIds?>((film, _) => ids.Apply(film))
            .Returns(true);

        // Act
        var result = await Create(new FilmCreateRequest
        {
            Title = title,
            IdentityDecision = "select",
            SelectedIdentityKey = key
        });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Accepted);
        var film = Stored(result.FilmId!.Value)!;
        film.Name.Should().Be(title);
        film.TmdbId.Should().Be(tmdbId);
        film.ImdbId.Should().Be(imdbId);
        film.Release!.Date.Should().Be(date);
    }

    [Fact(DisplayName =
        "A title whose chosen identity already belongs to a film is a conflict naming that film and writes nothing, because the identity gate must not admit a duplicate.")]
    public async Task selected_identity_clash_is_conflict()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var tmdbId = _fixture.CreateTmdbId();
        var existing = SeedFilm(f => f.TmdbId = tmdbId);
        var ids = new CatalogueAuthorityIds(null, tmdbId, null);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                title, null, CatalogueIdentityChoice.Unset, null, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed(ids));
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ApplyMissing(It.IsAny<Film>(), ids))
            .Callback<Film, CatalogueAuthorityIds?>((film, _) => ids.Apply(film))
            .Returns(true);

        // Act
        var result = await Create(new FilmCreateRequest { Title = title });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Conflict);
        result.FilmId.Should().Be(existing.Id);
        _created.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A title with identityDecision cancel is Cancelled and writes nothing.")]
    public async Task cancel_writes_nothing()
    {
        // Arrange
        var title = _fixture.CreateTitle();

        // Act
        var result = await Create(new FilmCreateRequest { Title = title, IdentityDecision = "cancel" });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Cancelled);
        _created.Should().BeEmpty();
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A request with no ids and no title is a bad request, because there is nothing to create or look up.")]
    public async Task nothing_to_create_is_bad_request()
    {
        // Arrange
        var request = new FilmCreateRequest { Publisher = _fixture.CreateTitle() };

        // Act
        var result = await Create(request);

        // Assert
        result.Status.Should().Be(FilmCreateStatus.BadRequest);
        _created.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An IMDb id that is not 'tt' followed by digits is a bad request and never reaches TMDB.")]
    public async Task malformed_imdb_id_is_bad_request()
    {
        // Arrange
        var request = new FilmCreateRequest { ImdbId = _fixture.CreateTitle() };

        // Act
        var result = await Create(request);

        // Assert
        result.Status.Should().Be(FilmCreateStatus.BadRequest);
        _mocker.GetMock<IIdentityMetadataEnricher>().Verify(
            e => e.FetchFilmAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A film name containing '/' is a bad request, because film names are used in paths; the curator sends a title.")]
    public async Task name_with_slash_is_bad_request()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        ArrangeIdentity(tmdbId);
        var title = $"{_fixture.CreateTitle()}/{_fixture.CreateTitle()}";

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId, Title = title });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.BadRequest);
        _created.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the search push fails the film is kept and the result reports failureIndexing with the film id, because the film exists and a later film save or push indexes it.")]
    public async Task search_failure_keeps_film_and_reports_indexing_failure()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        ArrangeIdentity(tmdbId);
        _searchUploadSucceeds = false;

        // Act
        var result = await Create(new FilmCreateRequest { TmdbId = tmdbId });

        // Assert
        result.Status.Should().Be(FilmCreateStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        Stored(result.FilmId!.Value).Should().NotBeNull();
    }
}
