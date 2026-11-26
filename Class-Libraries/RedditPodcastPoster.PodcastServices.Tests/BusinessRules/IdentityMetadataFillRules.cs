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
        "A film's values that are already set are never overwritten by TMDB: description, release, runtime, image, " +
        "alternative titles and TMDB id all stay as the curator or the streaming service set them.")]
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

        // Act
        var changed = IdentityMetadataFill.Fill(film, Movie(film.Name));

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
        "Fill does not store a TMDB image over that service art (RPP #1497).")]
    public void service_image_closes_the_image_gap()
    {
        // Arrange
        var serviceImage = new Uri($"https://i.ytimg.com/vi/{_fixture.CreateYouTubeId()}/hqdefault.jpg");
        var film = new Film(_fixture.CreateTitle())
        {
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            AlternativeTitles = [],
            PublisherName = "A24",
            Services = new Dictionary<string, ServiceLink>
            {
                ["youtube"] = new() { Image = serviceImage }
            }
        };
        film.SetRelease(CatalogueRelease.FromYear(2001));
        var metadata = Movie(film.Name);

        // Act
        var gaps = IdentityMetadataFill.HasGaps(film);
        IdentityMetadataFill.Fill(film, metadata);

        // Assert
        gaps.Should().BeFalse();
        film.Image.Should().BeNull();
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
