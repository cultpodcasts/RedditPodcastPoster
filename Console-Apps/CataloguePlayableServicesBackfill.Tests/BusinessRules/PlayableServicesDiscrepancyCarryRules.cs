using CataloguePlayableServicesBackfill;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace CataloguePlayableServicesBackfill.Tests.BusinessRules;

public class PlayableServicesDiscrepancyCarryRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Playable services carry: when backup and live url and image match, then there are no changes, " +
        "because identical services must not be rewritten.")]
    public void identical_is_noop()
    {
        // Arrange
        var url = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var image = new Uri($"https://img.example/{_fixture.CreateGuid():N}.jpg");
        var live = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = url, Image = image }
        };
        var backup = PlayableServicesDiscrepancyCarry.CloneServices(live)!;

        // Act
        var result = PlayableServicesDiscrepancyCarry.Apply(live, backup);

        // Assert
        result.HasChanges.Should().BeFalse();
        result.Changes.Should().BeEmpty();
        result.Services![ServiceKeys.Spotify].Url.Should().Be(url);
        result.Services[ServiceKeys.Spotify].Image.Should().Be(image);
    }

    [Fact(DisplayName =
        "Playable services carry: when live url is null and backup has a url, then the backup url is carried, " +
        "because episode backup is the source of truth for stripped services.")]
    public void null_live_url_takes_backup()
    {
        // Arrange
        var backupUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var live = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = null, Image = null }
        };
        var backup = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = backupUrl }
        };

        // Act
        var result = PlayableServicesDiscrepancyCarry.Apply(live, backup);

        // Assert
        result.HasChanges.Should().BeTrue();
        result.Changes.Should().ContainSingle(c => c.Key == ServiceKeys.Spotify && c.Field == "url");
        result.Services![ServiceKeys.Spotify].Url.Should().Be(backupUrl);
    }

    [Fact(DisplayName =
        "Playable services carry: when live and backup urls differ, then the backup url replaces live, " +
        "because discrepancies are carried from the episode JSON onto the entity.")]
    public void differing_url_is_replaced()
    {
        // Arrange
        var liveUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var backupUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var live = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = liveUrl }
        };
        var backup = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = backupUrl }
        };

        // Act
        var result = PlayableServicesDiscrepancyCarry.Apply(live, backup);

        // Assert
        result.Changes.Should().ContainSingle(c =>
            c.Field == "url" && c.Before == liveUrl.AbsoluteUri && c.After == backupUrl.AbsoluteUri);
        result.Services![ServiceKeys.YouTube].Url.Should().Be(backupUrl);
    }

    [Fact(DisplayName =
        "Playable services carry: when backup has an image and live does not, then the image is carried, " +
        "because image urls were stripped alongside service urls.")]
    public void missing_image_is_carried()
    {
        // Arrange
        var url = _fixture.CreateAppleCatalogueInput().AppleUrl;
        var image = new Uri($"https://img.example/{_fixture.CreateGuid():N}.jpg");
        var live = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Apple] = new() { Url = url, Image = null }
        };
        var backup = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Apple] = new() { Url = url, Image = image }
        };

        // Act
        var result = PlayableServicesDiscrepancyCarry.Apply(live, backup);

        // Assert
        result.Changes.Should().ContainSingle(c => c.Field == "image");
        result.Services![ServiceKeys.Apple].Image.Should().Be(image);
        result.Services[ServiceKeys.Apple].Url.Should().Be(url);
    }

    [Fact(DisplayName =
        "Playable services carry: when live url and image are set and backup has the same key with null fields, " +
        "then there are no changes and live values are kept, because a null backup must not wipe enriched live services.")]
    public void null_backup_fields_do_not_wipe_live()
    {
        // Arrange
        var liveUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var liveImage = new Uri($"https://img.example/{_fixture.CreateGuid():N}.jpg");
        var live = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = liveUrl, Image = liveImage }
        };
        var backup = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = null, Image = null }
        };

        // Act
        var result = PlayableServicesDiscrepancyCarry.Apply(live, backup);

        // Assert
        result.HasChanges.Should().BeFalse();
        result.Changes.Should().BeEmpty();
        result.Services![ServiceKeys.Spotify].Url.Should().Be(liveUrl);
        result.Services[ServiceKeys.Spotify].Image.Should().Be(liveImage);
    }

    [Fact(DisplayName =
        "Playable services carry: when backup url differs but backup image is null, then only the url is carried " +
        "and the live image is preserved, because null backup fields must not wipe live.")]
    public void null_backup_image_preserves_live_image_when_url_carries()
    {
        // Arrange
        var liveUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var backupUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var liveImage = new Uri($"https://img.example/{_fixture.CreateGuid():N}.jpg");
        var live = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = liveUrl, Image = liveImage }
        };
        var backup = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = backupUrl, Image = null }
        };

        // Act
        var result = PlayableServicesDiscrepancyCarry.Apply(live, backup);

        // Assert
        result.Changes.Should().ContainSingle(c => c.Field == "url");
        result.Changes.Should().NotContain(c => c.Field == "image");
        result.Services![ServiceKeys.YouTube].Url.Should().Be(backupUrl);
        result.Services[ServiceKeys.YouTube].Image.Should().Be(liveImage);
    }

    [Fact(DisplayName =
        "Playable services carry: when live has a service key absent from backup, then that key is left alone, " +
        "because only backup keys are candidates for carry.")]
    public void live_only_key_is_preserved()
    {
        // Arrange
        var liveOnlyUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var backupUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var live = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = liveOnlyUrl },
            [ServiceKeys.YouTube] = new()
        };
        var backup = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = backupUrl }
        };

        // Act
        var result = PlayableServicesDiscrepancyCarry.Apply(live, backup);

        // Assert
        result.Services![ServiceKeys.Spotify].Url.Should().Be(liveOnlyUrl);
        result.Services[ServiceKeys.YouTube].Url.Should().Be(backupUrl);
    }
}
