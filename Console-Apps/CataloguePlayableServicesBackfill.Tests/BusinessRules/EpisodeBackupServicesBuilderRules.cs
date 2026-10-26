using CataloguePlayableServicesBackfill;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace CataloguePlayableServicesBackfill.Tests.BusinessRules;

public class EpisodeBackupServicesBuilderRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Episode backup services builder: when services is empty but leftover urls has spotify, " +
        "then the built map includes that spotify url, because leftover urls backfill empty services.")]
    public void leftover_url_fills_empty_services()
    {
        // Arrange
        var url = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var document = new EpisodeBackupDocument
        {
            Services = null,
            Urls = new ServiceUrls { Spotify = url }
        };

        // Act
        var map = EpisodeBackupServicesBuilder.Build(document);

        // Assert
        map.Should().ContainKey(ServiceKeys.Spotify);
        map[ServiceKeys.Spotify].Url.Should().Be(url);
    }

    [Fact(DisplayName =
        "Episode backup services builder: when services already has a url, leftover urls does not overwrite it, " +
        "because catalog services on the backup win over leftover.")]
    public void services_url_wins_over_leftover()
    {
        // Arrange
        var servicesUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var leftoverUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var document = new EpisodeBackupDocument
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = servicesUrl }
            },
            Urls = new ServiceUrls { Spotify = leftoverUrl }
        };

        // Act
        var map = EpisodeBackupServicesBuilder.Build(document);

        // Assert
        map[ServiceKeys.Spotify].Url.Should().Be(servicesUrl);
    }

    [Fact(DisplayName =
        "Episode backup services builder: when services has a url without image and leftover images has one, " +
        "then the image is filled, because leftover images complete backup services.")]
    public void leftover_image_fills_missing_image()
    {
        // Arrange
        var url = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var image = new Uri($"https://img.example/{_fixture.CreateGuid():N}.jpg");
        var document = new EpisodeBackupDocument
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = url }
            },
            Images = new EpisodeImages { Spotify = image }
        };

        // Act
        var map = EpisodeBackupServicesBuilder.Build(document);

        // Assert
        map[ServiceKeys.Spotify].Url.Should().Be(url);
        map[ServiceKeys.Spotify].Image.Should().Be(image);
    }
}
