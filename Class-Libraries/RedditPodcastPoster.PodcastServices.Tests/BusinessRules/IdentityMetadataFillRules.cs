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
            TmdbId = tmdbId
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
        "the identity image is still stored when the film has none of its own.")]
    public void service_image_closes_the_image_gap()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle())
        {
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            AlternativeTitles = [],
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
        var release = CatalogueRelease.FromDateTimeUtc(new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc));
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
        "writing blanks or zeroes.")]
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
        filmChanged.Should().BeFalse();
        episodeChanged.Should().BeFalse();
        film.Release.Should().BeNull();
        film.Length.Should().Be(TimeSpan.Zero);
        film.AlternativeTitles.Should().BeNull();
        episode.Release.Should().BeNull();
        episode.Image.Should().BeNull();
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
            [_fixture.CreateTitle(), _fixture.CreateTitle()]);

    private TmdbEpisodeMetadata Episode() =>
        new(
            _fixture.CreateTmdbId(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            20 + _fixture.CreateTmdbId() % 40,
            new DateOnly(2023, 9, 1 + _fixture.CreateTmdbId() % 28),
            new Uri($"https://image.tmdb.org/t/p/w780/{_fixture.CreateYouTubeId()}.jpg"));
}
