using FluentAssertions;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Tests;

public class CatalogueContentTypeModelRulesTests
{
    private static readonly string[] ForbiddenProviderIdProperties =
        ["Ids", "YouTubeId", "SpotifyId", "AppleId", "YoutubeId"];

    [Fact(DisplayName =
        "ContentKind enumerates the four playable kinds: Episode, TvShowEpisode, Film, NewsReport.")]
    public void ContentKind_has_expected_playable_values()
    {
        // Arrange
        var names = Enum.GetNames<ContentKind>();

        // Act
        var values = Enum.GetValues<ContentKind>();

        // Assert
        names.Should().BeEquivalentTo(["Episode", "TvShowEpisode", "Film", "NewsReport"]);
        values.Should().HaveCount(4);
        values.Should().Contain(ContentKind.Episode);
        values.Should().Contain(ContentKind.TvShowEpisode);
        values.Should().Contain(ContentKind.Film);
        values.Should().Contain(ContentKind.NewsReport);
    }

    [Fact(DisplayName =
        "Film, TvShow, TvShowEpisode, NewsOrganisation, and NewsReport are CosmosSelector types with matching ModelType.")]
    public void Catalogue_entities_are_cosmos_selectors_with_model_type()
    {
        // Arrange / Act / Assert
        typeof(Film).Should().BeAssignableTo<CosmosSelector>();
        typeof(TvShow).Should().BeAssignableTo<CosmosSelector>();
        typeof(TvShowEpisode).Should().BeAssignableTo<CosmosSelector>();
        typeof(NewsOrganisation).Should().BeAssignableTo<CosmosSelector>();
        typeof(NewsReport).Should().BeAssignableTo<CosmosSelector>();

        new Film().ModelType.Should().Be(ModelType.Film);
        new TvShow().ModelType.Should().Be(ModelType.TvShow);
        new TvShowEpisode().ModelType.Should().Be(ModelType.TvShowEpisode);
        new NewsOrganisation().ModelType.Should().Be(ModelType.NewsOrganisation);
        new NewsReport().ModelType.Should().Be(ModelType.NewsReport);
    }

    [Fact(DisplayName =
        "Film has no parent id property because a film is a standalone playable with no series parent.")]
    public void Film_has_no_parent_id_property()
    {
        // Arrange
        var filmType = typeof(Film);
        var forbiddenParentIds = new[] { "NewsOrganisationId", "TvShowId", "PodcastId" };

        // Act
        var propertyNames = filmType.GetProperties().Select(p => p.Name).ToArray();

        // Assert
        propertyNames.Should().NotContain(forbiddenParentIds);
    }

    [Fact(DisplayName =
        "Film, TvShowEpisode, and NewsReport have no provider-id properties (YouTube/Spotify/Apple/Ids): " +
        "platform presence is services only, unlike podcast Episode collection identity.")]
    public void Non_podcast_playables_have_no_provider_id_fields()
    {
        // Arrange
        var playableTypes = new[] { typeof(Film), typeof(TvShowEpisode), typeof(NewsReport) };

        // Act / Assert
        foreach (var type in playableTypes)
        {
            var propertyNames = type.GetProperties().Select(p => p.Name).ToArray();
            propertyNames.Should().NotContain(ForbiddenProviderIdProperties, because: type.Name);
            propertyNames.Should().Contain("Services", because: type.Name);
            type.GetProperty("Services")!.PropertyType
                .Should().Be(typeof(Dictionary<string, ServiceLink>), because: type.Name);
        }
    }

    [Fact(DisplayName =
        "Film release may be year-only or a calendar date; TvShowEpisode and NewsReport release are calendar date — " +
        "none use a podcast-episode DateTime release.")]
    public void Non_podcast_playables_use_catalogue_release_not_datetime()
    {
        // Arrange
        var yearRelease = CatalogueRelease.FromYear(2020);
        var dateRelease = CatalogueRelease.FromDate(new DateOnly(2020, 6, 15));

        // Act
        var filmReleaseType = typeof(Film).GetProperty(nameof(Film.Release))!.PropertyType;
        var tvReleaseType = typeof(TvShowEpisode).GetProperty(nameof(TvShowEpisode.Release))!.PropertyType;
        var newsReleaseType = typeof(NewsReport).GetProperty(nameof(NewsReport.Release))!.PropertyType;

        // Assert
        filmReleaseType.Should().Be(typeof(CatalogueRelease));
        tvReleaseType.Should().Be(typeof(CatalogueRelease));
        newsReleaseType.Should().Be(typeof(CatalogueRelease));

        yearRelease.Precision.Should().Be(CatalogueReleasePrecision.Year);
        yearRelease.Year.Should().Be(2020);
        yearRelease.Date.Should().BeNull();

        dateRelease.Precision.Should().Be(CatalogueReleasePrecision.Date);
        dateRelease.Date.Should().Be(new DateOnly(2020, 6, 15));
        dateRelease.Year.Should().Be(2020);
    }

    [Fact(DisplayName =
        "Film, TvShow, and NewsOrganisation file keys use kind prefixes (film-/tvshow-/news-) so public JSON DB " +
        "and backups do not collide with unprefixed podcast series keys.")]
    public void Series_like_entities_use_prefixed_file_keys()
    {
        // Arrange
        var name = "Example Show";

        // Act
        var film = new Film(name);
        var tvShow = new TvShow(name);
        var newsOrg = new NewsOrganisation(name);
        var slug = FileKeyFactory.GetFileKey(name);

        // Assert
        film.FileKey.Should().Be($"{FileKeyFactory.FilmPrefix}{slug}");
        tvShow.FileKey.Should().Be($"{FileKeyFactory.TvShowPrefix}{slug}");
        newsOrg.FileKey.Should().Be($"{FileKeyFactory.NewsOrganisationPrefix}{slug}");
        slug.Should().NotStartWith("film-");
    }

    [Fact(DisplayName =
        "TvShowEpisode.SetTvShowProperties updates TvShowId and TvShowName from the parent show.")]
    public void TvShowEpisode_SetTvShowProperties_updates_id_and_name()
    {
        // Arrange
        var tvShowId = Guid.NewGuid();
        var tvShowName = Guid.NewGuid().ToString("N");
        var tvShow = new TvShow { Id = tvShowId, Name = $" {tvShowName} " };
        var episode = new TvShowEpisode();

        // Act
        var updated = episode.SetTvShowProperties(tvShow);

        // Assert
        updated.Should().BeTrue();
        episode.TvShowId.Should().Be(tvShowId);
        episode.TvShowName.Should().Be(tvShowName);
    }
}
