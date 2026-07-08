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

    [Theory(DisplayName =
        "When only the platform URL is missing and id/image are already set, the applier fills only the URL.")]
    [MemberData(nameof(AllPlatformServices))]
    public void applier_fills_missing_url_only_when_id_and_image_present(Service service)
    {
        // Arrange
        var (target, patch, expected) = CreateMissingUrlOnlyScenario(service);

        // Act
        var updated = _applier.ApplyFillMissing(target, patch);

        // Assert
        updated.Should().BeTrue();
        target.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When a stored description ends with ellipsis and the patch supplies a longer description, " +
        "the applier replaces the truncated description.")]
    public void applier_extends_truncated_description_ending_in_ellipsis()
    {
        // Arrange
        const string truncated = "Short preview...";
        const string full = "Short preview with the complete episode summary.";
        var podcast = _fixture.CreatePodcast();
        var target = _fixture.CreateStoredEpisode(podcast, e => e.Description = truncated);
        var patch = new EpisodePlatformPatch(null, full, null);

        // Act
        var updated = _applier.ApplyFillMissing(target, patch);

        // Assert
        updated.Should().BeTrue();
        target.Description.Should().Be(full);
    }

    [Fact(DisplayName =
        "When a stored description is complete, the applier does not replace it with patch description.")]
    public void applier_does_not_replace_complete_description()
    {
        // Arrange
        const string complete = "A complete episode description without truncation.";
        var podcast = _fixture.CreatePodcast();
        var target = _fixture.CreateStoredEpisode(podcast, e => e.Description = complete);
        var patch = new EpisodePlatformPatch(null, "A much longer replacement description.", null);

        // Act
        var updated = _applier.ApplyFillMissing(target, patch);

        // Assert
        updated.Should().BeFalse();
        target.Description.Should().Be(complete);
    }

    [Fact(DisplayName =
        "ApplyFillMissingRelease updates the stored release when the incoming release differs.")]
    public void applier_updates_release_when_different()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var target = _fixture.CreateStoredEpisode(podcast);
        var newRelease = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());

        // Act
        var updated = _applier.ApplyFillMissingRelease(target, newRelease);

        // Assert
        updated.Should().BeTrue();
        target.Release.Should().Be(newRelease);
    }

    [Fact(DisplayName =
        "ApplyFillMissingRelease leaves the stored release unchanged when values are equal.")]
    public void applier_does_not_update_release_when_equal()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var target = _fixture.CreateStoredEpisode(podcast);
        var existing = target.Release;

        // Act
        var updated = _applier.ApplyFillMissingRelease(target, existing);

        // Assert
        updated.Should().BeFalse();
        target.Release.Should().Be(existing);
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

    private (Episode Target, EpisodePlatformPatch Patch, EpisodeExpectation Expected)
        CreateMissingUrlOnlyScenario(Service service)
    {
        var podcast = _fixture.CreatePodcast();
        var existingLink = CreatePlatformLink(service);
        var incomingUrl = CreatePlatformLink(service).Url;

        var target = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages();
            })
            .Create();

        ApplyExistingLink(target, existingLink);
        ClearUrl(target, service);
        var expected = EpisodeExpectation.From(target);
        var patch = new EpisodePlatformPatch(
            existingLink with { Url = incomingUrl },
            Description: null,
            Release: null);

        return service switch
        {
            Service.Spotify => (target, patch, expected.WithSpotify(existingLink.Id!, incomingUrl!, existingLink.Image)),
            Service.Apple => (target, patch, expected.WithApple(long.Parse(existingLink.Id!), incomingUrl!, existingLink.Image)),
            Service.YouTube => (target, patch, expected.WithYouTube(existingLink.Id!, incomingUrl!, existingLink.Image)),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
        };
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

    private static void ClearUrl(Episode target, Service service)
    {
        switch (service)
        {
            case Service.Spotify:
                target.Urls.Spotify = null;
                break;
            case Service.Apple:
                target.Urls.Apple = null;
                break;
            case Service.YouTube:
                target.Urls.YouTube = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(service), service, null);
        }
    }

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
