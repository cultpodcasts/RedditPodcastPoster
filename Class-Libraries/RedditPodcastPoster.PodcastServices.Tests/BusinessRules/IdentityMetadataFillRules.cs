using FluentAssertions;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class IdentityMetadataFillRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A film with only a name and identity ids is filled from TMDB: description, release, runtime, image, alternative " +
        "titles and a missing TMDB id, because a film created from its trailer has nothing else to show (RPP #1430).")]
    public void empty_film_is_filled()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { ImdbId = _fixture.CreateImdbId(), Description = string.Empty };
        var metadata = Movie(film.Name);

        // Act
        var changed = IdentityMetadataFill.Fill(film, metadata);

        // Assert
        changed.Should().BeTrue();
        film.TmdbId.Should().Be(metadata.Id);
        film.Description.Should().Be(metadata.Overview);
        film.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(metadata.ReleaseDate!.Value));
        film.ReleaseSort.Should().Be(metadata.ReleaseDate!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        film.Length.Should().Be(TimeSpan.FromMinutes(metadata.RuntimeMinutes!.Value));
        film.Image.Should().Be(metadata.Image);
        film.AlternativeTitles.Should().Equal(metadata.OtherTitles);
        film.PublisherName.Should().Be(metadata.Publisher);
    }

    [Fact(DisplayName =
        "A film's values that are already set are never overwritten by TMDB when the release year is already in the " +
        "identity year (within +1): description, release, runtime, image, alternative titles and TMDB id all stay as " +
        "the curator or the streaming service set them.")]
    public void set_film_fields_are_kept()
    {
        // Arrange
        var description = _fixture.CreateTitle();
        var release = CatalogueRelease.FromYear(1999);
        var length = _fixture.CreateDuration();
        var image = new Uri($"https://example.org/{_fixture.CreateYouTubeId()}.jpg");
        var titles = new List<string> { _fixture.CreateTitle() };
        var tmdbId = _fixture.CreateTmdbId();
        var film = new Film(_fixture.CreateTitle())
        {
            Description = description,
            Length = length,
            Image = image,
            AlternativeTitles = titles,
            TmdbId = tmdbId,
            PublisherName = "A24"
        };
        film.SetRelease(release);
        // Identity year must be same-band as the stored year so #1410 does not treat this as an upload date.
        var metadata = Movie(film.Name) with { ReleaseDate = new DateOnly(1999, 10, 4), ReleaseYear = 1999 };

        // Act
        var changed = IdentityMetadataFill.Fill(film, metadata);

        // Assert
        changed.Should().BeFalse();
        film.Description.Should().Be(description);
        film.Release.Should().Be(release);
        film.Length.Should().Be(length);
        film.Image.Should().Be(image);
        film.AlternativeTitles.Should().Equal(titles);
        film.TmdbId.Should().Be(tmdbId);
        IdentityMetadataFill.HasGaps(film).Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a film's release is a platform upload datetime more than one year after the identity year, identity " +
        "replaces it with the TMDB calendar date so Move to film does not keep the YouTube upload day (RPP #1410).")]
    public void upload_datetime_release_is_replaced_by_identity_date()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle())
        {
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            AlternativeTitles = [],
            PublisherName = "A24"
        };
        film.SetRelease(CatalogueRelease.FromDateTimeUtc(
            DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay())));
        var identityDate = new DateOnly(1981, 10, 4);
        var metadata = Movie(film.Name) with { ReleaseDate = identityDate, ReleaseYear = identityDate.Year };

        // Act
        var gaps = IdentityMetadataFill.HasGaps(film);
        var changed = IdentityMetadataFill.Fill(film, metadata);

        // Assert
        gaps.Should().BeTrue();
        changed.Should().BeTrue();
        film.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(identityDate));
    }

    [Fact(DisplayName =
        "A film whose stored release year is exactly one year after the identity year is kept: the +1 tolerance " +
        "treats a this-year upload of a last-year release as the curator's date, so identity fill must not overwrite it (RPP #1410).")]
    public void release_one_year_after_identity_is_kept()
    {
        // Arrange
        var identityYear = DomainTestFixture.UtcToday.Year - 1;
        var stored = CatalogueRelease.FromYear(identityYear + 1);
        var film = FilmWithSetFields(stored);
        var metadata = Movie(film.Name) with
        {
            ReleaseDate = new DateOnly(identityYear, 1, 1),
            ReleaseYear = identityYear
        };

        // Act
        var replace = IdentityMetadataFill.ShouldReplaceFilmRelease(film.Release, identityYear);
        var changed = IdentityMetadataFill.Fill(film, metadata);

        // Assert
        replace.Should().BeFalse();
        changed.Should().BeFalse();
        film.Release.Should().Be(stored);
    }

    [Fact(DisplayName =
        "A film whose stored release year is two years after the identity year is replaced by the identity date, " +
        "because that is past the one-year tolerance for a platform upload (RPP #1410).")]
    public void release_two_years_after_identity_is_replaced()
    {
        // Arrange
        var identityYear = DomainTestFixture.UtcToday.Year - 2;
        var stored = CatalogueRelease.FromYear(identityYear + 2);
        var film = FilmWithSetFields(stored);
        var identityDate = new DateOnly(identityYear, 1, 1);
        var metadata = Movie(film.Name) with { ReleaseDate = identityDate, ReleaseYear = identityYear };

        // Act
        var replace = IdentityMetadataFill.ShouldReplaceFilmRelease(film.Release, identityYear);
        var changed = IdentityMetadataFill.Fill(film, metadata);

        // Assert
        replace.Should().BeTrue();
        changed.Should().BeTrue();
        film.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(identityDate));
    }

    [Fact(DisplayName =
        "When identity only has a year (no full yyyy-MM-dd), the film release is stored as year precision, not as " +
        "1 January (RPP #1410).")]
    public void year_only_identity_date_is_stored_as_year_precision()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Description = string.Empty };
        var metadata = Movie(film.Name) with { ReleaseDate = null, ReleaseYear = 1994 };

        // Act
        IdentityMetadataFill.Fill(film, metadata);

        // Assert
        film.Release.Should().BeEquivalentTo(CatalogueRelease.FromYear(1994));
        film.Release!.Precision.Should().Be(CatalogueReleasePrecision.Year);
    }

    [Fact(DisplayName =
        "An explicit release skip (curator set release in the same PATCH) leaves a platform upload datetime alone " +
        "even when the identity year is earlier (RPP #1410).")]
    public void skipped_release_is_not_replaced_by_identity()
    {
        // Arrange
        var upload = CatalogueRelease.FromDateTimeUtc(
            DomainTestFixture.UtcAtTime(-5, _fixture.CreateNonMidnightTimeOfDay()));
        var film = new Film(_fixture.CreateTitle()) { Description = string.Empty };
        film.SetRelease(upload);
        var metadata = Movie(film.Name) with { ReleaseDate = new DateOnly(1960, 12, 4), ReleaseYear = 1960 };

        // Act
        IdentityMetadataFill.Fill(film, metadata, new IdentityFieldSkip(Release: true));

        // Assert
        film.Release.Should().Be(upload);
    }

    [Fact(DisplayName =
        "Alternative titles a curator cleared ([]) are not filled again, because the curator chose to have none; " +
        "titles never set (null) are filled.")]
    public void cleared_alternative_titles_stay_cleared()
    {
        // Arrange
        var cleared = new Film(_fixture.CreateTitle()) { AlternativeTitles = [] };
        var neverSet = new Film(_fixture.CreateTitle());

        // Act
        IdentityMetadataFill.Fill(cleared, Movie(cleared.Name));
        IdentityMetadataFill.Fill(neverSet, Movie(neverSet.Name));

        // Assert
        cleared.AlternativeTitles.Should().BeEmpty();
        neverSet.AlternativeTitles.Should().NotBeEmpty();
    }

    [Fact(DisplayName =
        "A film's alternative titles drop its own name and repeats (case-insensitive), so search does not count the " +
        "name twice.")]
    public void alternative_titles_drop_the_name_and_repeats()
    {
        // Arrange
        var film = new Film("Faderhuset");
        var metadata = Movie("Father's House") with
        {
            OtherTitles = ["faderhuset", "Father's House", "FATHER'S HOUSE", "La casa del padre"]
        };

        // Act
        IdentityMetadataFill.Fill(film, metadata);

        // Assert
        film.AlternativeTitles.Should().Equal("Father's House", "La casa del padre");
    }

    [Fact(DisplayName =
        "A film whose streaming service has an image has no image gap, because the service image wins in search; " +
        "the identity image is still stored when the film has none of its own.")]
    public void service_image_closes_the_image_gap()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle())
        {
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            AlternativeTitles = [],
            PublisherName = "A24",
            Services = new Dictionary<string, ServiceLink>
            {
                ["youtube"] = new() { Image = new Uri($"https://i.ytimg.com/vi/{_fixture.CreateYouTubeId()}/hqdefault.jpg") }
            }
        };
        film.SetRelease(CatalogueRelease.FromYear(2001));

        // Act
        var gaps = IdentityMetadataFill.HasGaps(film);

        // Assert
        gaps.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A TV episode with empty fields is filled from TMDB: name, overview, air date, runtime, still and a missing " +
        "TMDB episode id (RPP #1430).")]
    public void empty_tv_episode_is_filled()
    {
        // Arrange
        var episode = new TvShowEpisode { TvdbId = _fixture.CreateTvdbId(), Title = string.Empty, Description = " " };
        var metadata = Episode();

        // Act
        var changed = IdentityMetadataFill.Fill(episode, metadata);

        // Assert
        changed.Should().BeTrue();
        episode.TmdbId.Should().Be(metadata.Id);
        episode.Title.Should().Be(metadata.Name);
        episode.Description.Should().Be(metadata.Overview);
        episode.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(metadata.AirDate!.Value));
        episode.Length.Should().Be(TimeSpan.FromMinutes(metadata.RuntimeMinutes!.Value));
        episode.Image.Should().Be(metadata.Still);
    }

    [Fact(DisplayName =
        "A TV episode's values that are already set are never overwritten by TMDB: name, overview, air date, runtime, " +
        "image and TMDB id stay as they were.")]
    public void set_tv_episode_fields_are_kept()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.CreateTitle();
        var release = CatalogueRelease.FromDateTimeUtc(DomainTestFixture.UtcAtTime(-400, _fixture.CreateNonMidnightTimeOfDay()));
        var length = _fixture.CreateDuration();
        var image = new Uri($"https://example.org/{_fixture.CreateYouTubeId()}.jpg");
        var tmdbId = _fixture.CreateTmdbId();
        var episode = new TvShowEpisode(title)
        {
            Description = description,
            Length = length,
            Image = image,
            TmdbId = tmdbId
        };
        episode.SetRelease(release);

        // Act
        var changed = IdentityMetadataFill.Fill(episode, Episode());

        // Assert
        changed.Should().BeFalse();
        episode.Title.Should().Be(title);
        episode.Description.Should().Be(description);
        episode.Release.Should().Be(release);
        episode.Length.Should().Be(length);
        episode.Image.Should().Be(image);
        episode.TmdbId.Should().Be(tmdbId);
        IdentityMetadataFill.HasGaps(episode).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Metadata TMDB does not have (no overview, runtime, date or image) leaves the empty fields empty instead of " +
        "writing blanks or zeroes, and stores an empty alternative-title list so the next edit does not call TMDB again.")]
    public void missing_metadata_leaves_fields_empty()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle());
        var episode = new TvShowEpisode(_fixture.CreateTitle());

        // Act
        var filmChanged = IdentityMetadataFill.Fill(
            film, new TmdbMovieMetadata(0, film.Name, null, null, null, null, null, []));
        var episodeChanged = IdentityMetadataFill.Fill(
            episode, new TmdbEpisodeMetadata(0, null, null, null, null, null));

        // Assert
        filmChanged.Should().BeTrue();
        episodeChanged.Should().BeFalse();
        film.Release.Should().BeNull();
        film.Length.Should().Be(TimeSpan.Zero);
        film.AlternativeTitles.Should().NotBeNull().And.BeEmpty();
        IdentityMetadataFill.HasGaps(film).Should().BeTrue();
        episode.Release.Should().BeNull();
        episode.Image.Should().BeNull();
    }

    [Fact(DisplayName =
        "A successful TMDB lookup that finds no other titles stores [] so a later edit is not treated as having a titles " +
        "gap, while a curator's [] is still not refilled (RPP #1431).")]
    public void no_other_titles_are_stored_as_an_empty_list()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var film = new Film(name)
        {
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            Image = new Uri($"https://image.tmdb.org/t/p/w1280/{_fixture.CreateYouTubeId()}.jpg")
        };
        film.SetRelease(CatalogueRelease.FromYear(1999));
        var onlyOwnName = Movie(name) with { OtherTitles = [name, " "] };
        var cleared = new Film(_fixture.CreateTitle())
        {
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            Image = film.Image,
            TmdbId = _fixture.CreateTmdbId(),
            AlternativeTitles = [],
            PublisherName = "A24"
        };
        cleared.SetRelease(CatalogueRelease.FromYear(1999));

        // Act
        var marked = IdentityMetadataFill.Fill(film, onlyOwnName);
        var refilled = IdentityMetadataFill.Fill(cleared, Movie(cleared.Name));

        // Assert
        marked.Should().BeTrue();
        film.AlternativeTitles.Should().NotBeNull().And.BeEmpty();
        IdentityMetadataFill.HasGaps(film).Should().BeFalse();
        refilled.Should().BeFalse();
        cleared.AlternativeTitles.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName =
        "When a later PATCH changes the IMDb or TMDB id and does not send alternativeTitles, a [] list is treated as " +
        "fillable so the corrected id's titles are stored.")]
    public void empty_alternative_titles_are_refilled_when_the_identity_id_changes()
    {
        // Arrange
        var film = new Film("Faderhuset")
        {
            Description = _fixture.CreateTitle(),
            Length = TimeSpan.FromMinutes(90),
            Image = new Uri($"https://image.tmdb.org/t/p/w1280/{_fixture.CreateYouTubeId()}.jpg"),
            TmdbId = _fixture.CreateTmdbId(),
            AlternativeTitles = [],
            PublisherName = "A24"
        };
        film.SetRelease(CatalogueRelease.FromYear(1999));
        var metadata = Movie("Father's House") with { OtherTitles = ["Father's House", "La casa del padre"] };

        // Act
        var gapsWithoutRefill = IdentityMetadataFill.HasGaps(film);
        var gapsWithRefill = IdentityMetadataFill.HasGaps(film, refillEmptyAlternativeTitles: true);
        IdentityMetadataFill.Fill(film, metadata);
        var titlesAfterDefaultFill = film.AlternativeTitles!.ToList();
        var changed = IdentityMetadataFill.Fill(film, metadata, refillEmptyAlternativeTitles: true);

        // Assert
        gapsWithoutRefill.Should().BeFalse();
        gapsWithRefill.Should().BeTrue();
        titlesAfterDefaultFill.Should().BeEmpty();
        changed.Should().BeTrue();
        film.AlternativeTitles.Should().Equal("Father's House", "La casa del padre");
    }

    [Fact(DisplayName =
        "A field the curator cleared in this PATCH is not refilled from TMDB; other empty, untouched fields still are.")]
    public void skipped_fields_are_not_filled()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Description = string.Empty };
        var episode = new TvShowEpisode { Title = string.Empty, Description = " " };
        var skipFilm = new IdentityFieldSkip(Description: true);
        var skipEpisode = new IdentityFieldSkip(Title: true, Description: true);
        var movie = Movie("Father's House");
        var tv = Episode();

        // Act
        IdentityMetadataFill.Fill(film, movie, skipFilm);
        IdentityMetadataFill.Fill(episode, tv, skipEpisode);

        // Assert
        film.Description.Should().BeEmpty();
        film.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(movie.ReleaseDate!.Value));
        film.AlternativeTitles.Should().Equal(movie.OtherTitles);
        episode.Title.Should().BeEmpty();
        episode.Description.Should().Be(" ");
        episode.Image.Should().Be(tv.Still);
        IdentityMetadataFill.HasGaps(film, skipFilm).Should().BeFalse();
    }

    [Fact(DisplayName =
        "An empty film or TV show publisher is filled from TMDB (the first production company or network); a set or skipped publisher is kept (RPP #1436).")]
    public void publisher_is_filled_only_when_empty()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { PublisherName = "A24" };
        var emptyShow = new TvShow(_fixture.CreateTitle());
        var setShow = new TvShow(_fixture.CreateTitle()) { PublisherName = "HBO" };
        var metadata = new TmdbShowMetadata(_fixture.CreateTmdbId(), "BBC Two");

        // Act
        IdentityMetadataFill.Fill(film, Movie(film.Name));
        var showFilled = IdentityMetadataFill.Fill(emptyShow, metadata);
        var showKept = IdentityMetadataFill.Fill(setShow, metadata);
        IdentityMetadataFill.Fill(emptyShow, metadata, new IdentityFieldSkip(Publisher: true));

        // Assert
        film.PublisherName.Should().Be("A24");
        showFilled.Should().BeTrue();
        emptyShow.PublisherName.Should().Be("BBC Two");
        showKept.Should().BeFalse();
        setShow.PublisherName.Should().Be("HBO");
        IdentityMetadataFill.HasGaps(setShow).Should().BeFalse();
        IdentityMetadataFill.HasGaps(new TvShow(_fixture.CreateTitle()), new IdentityFieldSkip(Publisher: true))
            .Should().BeFalse();
    }

    private Film FilmWithSetFields(CatalogueRelease release)
    {
        var film = new Film(_fixture.CreateTitle())
        {
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            Image = new Uri($"https://example.org/{_fixture.CreateYouTubeId()}.jpg"),
            AlternativeTitles = [_fixture.CreateTitle()],
            TmdbId = _fixture.CreateTmdbId(),
            PublisherName = "A24"
        };
        film.SetRelease(release);
        return film;
    }

    private TmdbMovieMetadata Movie(string title) =>
        new(
            _fixture.CreateTmdbId(),
            title,
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            90 + _fixture.CreateTmdbId() % 60,
            new DateOnly(2024, 3, 1 + _fixture.CreateTmdbId() % 28),
            new Uri($"https://image.tmdb.org/t/p/w1280/{_fixture.CreateYouTubeId()}.jpg"),
            [_fixture.CreateTitle(), _fixture.CreateTitle()],
            "A24");

    private TmdbEpisodeMetadata Episode() =>
        new(
            _fixture.CreateTmdbId(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            20 + _fixture.CreateTmdbId() % 40,
            new DateOnly(2023, 9, 1 + _fixture.CreateTmdbId() % 28),
            new Uri($"https://image.tmdb.org/t/p/w780/{_fixture.CreateYouTubeId()}.jpg"));
}
