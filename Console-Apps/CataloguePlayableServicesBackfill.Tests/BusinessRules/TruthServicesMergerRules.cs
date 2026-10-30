using CataloguePlayableServicesBackfill;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace CataloguePlayableServicesBackfill.Tests.BusinessRules;

public class TruthServicesMergerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Truth services merge: when episode backup and public urls both have a key, then the backup url wins, " +
        "because the private episode export is the stronger truth for overlapping fields.")]
    public void backup_url_wins_over_public()
    {
        // Arrange
        var backupUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var publicUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var backup = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = backupUrl }
        };
        var publicUrls = new Dictionary<string, Uri>
        {
            [ServiceKeys.Spotify] = publicUrl
        };

        // Act
        var merged = TruthServicesMerger.Merge(backup, publicUrls);

        // Assert
        merged[ServiceKeys.Spotify].Url.Should().Be(backupUrl);
    }

    [Fact(DisplayName =
        "Truth services merge: when episode backup lacks a url that public has, then the public url fills the gap, " +
        "because PublicDatabase is a second truth source for service urls.")]
    public void public_fills_missing_url()
    {
        // Arrange
        var image = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var publicUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var backup = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Image = image }
        };
        var publicUrls = new Dictionary<string, Uri>
        {
            [ServiceKeys.Spotify] = publicUrl
        };

        // Act
        var merged = TruthServicesMerger.Merge(backup, publicUrls);

        // Assert
        merged[ServiceKeys.Spotify].Url.Should().Be(publicUrl);
        merged[ServiceKeys.Spotify].Image.Should().Be(image);
    }

    [Fact(DisplayName =
        "Truth services merge: when only public urls are present, then truth is url-only ServiceLinks, " +
        "because public files do not carry service images.")]
    public void public_only_builds_url_links()
    {
        // Arrange
        var publicUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var publicUrls = new Dictionary<string, Uri>
        {
            [ServiceKeys.YouTube] = publicUrl
        };

        // Act
        var merged = TruthServicesMerger.Merge(episodeBackup: null, publicUrls);

        // Assert
        merged.Should().ContainSingle();
        merged[ServiceKeys.YouTube].Url.Should().Be(publicUrl);
        merged[ServiceKeys.YouTube].Image.Should().BeNull();
    }
}
