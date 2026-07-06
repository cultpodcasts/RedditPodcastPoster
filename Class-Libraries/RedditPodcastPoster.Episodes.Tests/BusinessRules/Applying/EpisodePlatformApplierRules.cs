using FluentAssertions;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Applying;

/// <summary>
/// Layer 2 applier rules — fill-missing platform fields without overwriting existing values.
/// </summary>
public class EpisodePlatformApplierRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodePlatformApplier _applier = new();

    public static TheoryData<Service> AllPlatformServices() =>
        new()
        {
            Service.Spotify,
            Service.Apple,
            Service.YouTube
        };

    [Theory(DisplayName =
        "When platform ID, URL, and image are missing on a stored episode, the applier fills them " +
        "because enrichment must backfill absent platform links.")]
    [MemberData(nameof(AllPlatformServices))]
    public void applier_fills_missing_platform_id_url_and_image(Service service)
    {
        // Arrange
        var (target, patch, expected) = CreateMissingPlatformFieldsScenario(service);

        // Act
        var updated = _applier.ApplyFillMissing(target, patch);

        // Assert
        updated.Should().BeTrue();
        target.ShouldMatchExpectation(expected);
    }

    [Theory(DisplayName =
        "When platform ID, URL, and image are already set, the applier leaves them unchanged " +
        "because fill-missing must not replace existing platform links.")]
    [MemberData(nameof(AllPlatformServices))]
    public void applier_does_not_replace_existing_platform_id_url_or_image(Service service)
    {
        // Arrange
        var (target, patch, expected) = CreateExistingPlatformFieldsScenario(service);

        // Act
        var updated = _applier.ApplyFillMissing(target, patch);

        // Assert
        updated.Should().BeFalse();
        target.ShouldMatchExpectation(expected);
    }

    private (Episode Target, EpisodePlatformPatch Patch, EpisodeExpectation Expected)
        CreateMissingPlatformFieldsScenario(Service service)
    {
        var podcast = _fixture.CreatePodcast();
        var link = CreatePlatformLink(service);

        var target = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.SpotifyId = string.Empty;
                e.YouTubeId = string.Empty;
                e.AppleId = null;
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages();
            })
            .Create();

        var patch = new EpisodePlatformPatch(link, Description: null, Release: null);
        var expected = EpisodeExpectation.From(target);

        return service switch
        {
            Service.Spotify => (
                target,
                patch,
                expected.WithSpotify(link.Id!, link.Url!, link.Image)),
            Service.Apple => (
                target,
                patch,
                expected.WithApple(long.Parse(link.Id!), link.Url!, link.Image)),
            Service.YouTube => (
                target,
                patch,
                expected.WithYouTube(link.Id!, link.Url!, link.Image)),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
        };
    }

    private (Episode Target, EpisodePlatformPatch Patch, EpisodeExpectation Expected)
        CreateExistingPlatformFieldsScenario(Service service)
    {
        var podcast = _fixture.CreatePodcast();
        var existingLink = CreatePlatformLink(service);
        var incomingLink = CreatePlatformLink(service);

        var target = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages();
            })
            .Create();

        ApplyExistingLink(target, existingLink);
        var expected = EpisodeExpectation.From(target);
        var patch = new EpisodePlatformPatch(incomingLink, Description: null, Release: null);

        return (target, patch, expected);
    }

    private PlatformLink CreatePlatformLink(Service service) =>
        service switch
        {
            Service.Spotify =>
                new PlatformLink(
                    Service.Spotify,
                    _fixture.CreateSpotifyId(),
                    _fixture.CreateSpotifyCatalogueInput().SpotifyUrl,
                    _fixture.Create<Uri>()),
            Service.Apple =>
                new PlatformLink(
                    Service.Apple,
                    _fixture.CreateAppleId().ToString(),
                    _fixture.CreateAppleCatalogueInput().AppleUrl,
                    _fixture.Create<Uri>()),
            Service.YouTube =>
                new PlatformLink(
                    Service.YouTube,
                    _fixture.CreateYouTubeId(),
                    _fixture.CreateYouTubeCatalogueInput().YouTubeUrl,
                    _fixture.Create<Uri>()),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
        };

    private static void ApplyExistingLink(Episode target, PlatformLink link)
    {
        switch (link.Service)
        {
            case Service.Spotify:
                target.SpotifyId = link.Id!;
                target.Urls.Spotify = link.Url;
                target.Images!.Spotify = link.Image;
                break;
            case Service.Apple:
                target.AppleId = long.Parse(link.Id!);
                target.Urls.Apple = link.Url;
                target.Images!.Apple = link.Image;
                break;
            case Service.YouTube:
                target.YouTubeId = link.Id!;
                target.Urls.YouTube = link.Url;
                target.Images!.YouTube = link.Image;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(link.Service), link.Service, null);
        }
    }
}
