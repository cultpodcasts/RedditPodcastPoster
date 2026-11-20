using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Authority.Imdb;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

/// <summary>
/// #1387: a TV show the TV authorities cannot place may be a film or documentary on IMDb ("3 Days in Dimona").
/// Edit-time lookup offers such hits as low-confidence candidates, keyed so a pick never stores a TMDB movie id on a
/// TV show.
/// </summary>
public class CatalogueAuthorityTvShowOtherKindsRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();
    private readonly Dictionary<string, IReadOnlyList<ImdbSuggestion>> _imdb = new(StringComparer.OrdinalIgnoreCase);

    public CatalogueAuthorityTvShowOtherKindsRules()
    {
        _mocker.GetMock<IImdbSuggestionClient>()
            .Setup(client => client.SuggestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, CancellationToken _) =>
                _imdb.TryGetValue(query, out var suggestions) ? suggestions : []);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact(DisplayName =
        "A TV show whose full title finds nothing, but whose title before the colon is an IMDb film, gets that film " +
        "offered as a low-confidence candidate keyed by its IMDb id, with no ids applied (#1387).")]
    public async Task colon_title_offers_imdb_film_by_shorter_clause()
    {
        // Arrange
        var head = _fixture.CreateTitle();
        var name = $"{head}: {_fixture.CreateTitleNotContainedIn(head)}";
        var imdbId = _fixture.CreateImdbId();
        _imdb[head] = [Suggestion(imdbId, head, ImdbSuggestion.MovieType)];
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowOtherKindsAsync(name);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.DifferentTitle);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        var candidate = preview.Candidates.Should().ContainSingle().Subject;
        candidate.Key.Should().Be(CatalogueAuthorityCandidate.ImdbKey(imdbId));
        candidate.Kind.Should().Be(CatalogueAuthorityCandidate.FilmKind);
        candidate.TmdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "IMDb suggestions whose title does not agree with the TV show, and shorts, are not offered; with no TMDB movie " +
        "either, nothing is offered (#1387).")]
    public async Task unrelated_suggestions_are_not_offered()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        _imdb[name] =
        [
            Suggestion(_fixture.CreateImdbId(), _fixture.CreateTitleNotContainedIn(name), ImdbSuggestion.MovieType),
            Suggestion(_fixture.CreateImdbId(), name, "short")
        ];
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowOtherKindsAsync(name);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.NotInAuthority);
        preview.Candidates.Should().BeNullOrEmpty();
    }

    [Fact(DisplayName =
        "An IMDb series that TMDB knows is offered as its TMDB TV record, so a pick collects every TV id (#1387).")]
    public async Task imdb_series_known_to_tmdb_is_offered_as_tmdb_tv()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var imdbId = _fixture.CreateImdbId();
        var tmdbTvId = _fixture.CreateTmdbId();
        _imdb[name] = [Suggestion(imdbId, name, ImdbSuggestion.TvMiniSeriesType)];
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.FindTvSeriesByImdbIdAsync(imdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSearchHit(tmdbTvId, name, TmdbTitleKind.TvSeries, null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowOtherKindsAsync(name);

        // Assert
        var candidate = preview.Candidates.Should().ContainSingle().Subject;
        candidate.Key.Should().Be(CatalogueAuthorityCandidate.TmdbKey(tmdbTvId));
        candidate.Kind.Should().Be(CatalogueAuthorityCandidate.TvShowKind);
    }

    [Fact(DisplayName =
        "When IMDb has nothing, a TMDB movie with the same title is offered by its IMDb id and never by its TMDB movie " +
        "id; a TMDB movie with no IMDb id is dropped (#1387).")]
    public async Task tmdb_movie_is_offered_by_imdb_id_only()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var withImdb = _fixture.CreateTmdbId();
        var withoutImdb = withImdb + 1;
        var imdbId = _fixture.CreateImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TmdbSearchHit(withImdb, name, TmdbTitleKind.Movie, null),
                new TmdbSearchHit(withoutImdb, name, TmdbTitleKind.Movie, null)
            ]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectFilmAsync(withImdb, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, withImdb, null));
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectFilmAsync(withoutImdb, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(null, withoutImdb, null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowOtherKindsAsync(name);

        // Assert
        var candidate = preview.Candidates.Should().ContainSingle().Subject;
        candidate.Key.Should().Be(CatalogueAuthorityCandidate.ImdbKey(imdbId));
        candidate.Kind.Should().Be(CatalogueAuthorityCandidate.FilmKind);
        candidate.TmdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
    }

    private static ImdbSuggestion Suggestion(string imdbId, string title, string type) =>
        new(imdbId, title, type, null, null, null);
}
