using FluentAssertions;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Enrichment;

/// <summary>
/// Enrichment result â†’ context projection rules for indexing persistence tracking.
/// </summary>
public class PlatformEnrichmentResultExtensionsRules
{
    private readonly DomainTestFixture _fixture = new();

    public static TheoryData<Service> PlatformUrlServices() =>
        new()
        {
            Service.Spotify,
            Service.Apple,
            Service.YouTube
        };

    [Theory(DisplayName =
        "When a platform enrichment result carries a platform URL, ApplyTo marks the matching " +
        "enrichment context URL flag because persistence tracks per-service link updates.")]
    [MemberData(nameof(PlatformUrlServices))]
    public void apply_to_marks_matching_platform_url_flag(Service service)
    {
        // Arrange
        var url = CreatePlatformUrl(service);
        var result = new PlatformEnrichmentResult(
            Updated: true,
            Service: service,
            PlatformUrl: url,
            ReleaseUpdated: false,
            Release: null);
        var context = new EnrichmentContext();

        // Act
        result.ApplyTo(context);

        // Assert
        switch (service)
        {
            case Service.Spotify:
                context.SpotifyUrlUpdated.Should().BeTrue();
                context.AppleUrlUpdated.Should().BeFalse();
                context.YouTubeUrlUpdated.Should().BeFalse();
                break;
            case Service.Apple:
                context.AppleUrlUpdated.Should().BeTrue();
                context.SpotifyUrlUpdated.Should().BeFalse();
                context.YouTubeUrlUpdated.Should().BeFalse();
                break;
            case Service.YouTube:
                context.YouTubeUrlUpdated.Should().BeTrue();
                context.SpotifyUrlUpdated.Should().BeFalse();
                context.AppleUrlUpdated.Should().BeFalse();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(service), service, null);
        }
    }

    [Fact(DisplayName =
        "When a platform enrichment result reports a release update, ApplyTo propagates the release " +
        "to the enrichment context because persistence tracks release backfill separately.")]
    public void apply_to_propagates_release_when_release_updated()
    {
        // Arrange
        var release = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var url = _fixture.CreateAppleCatalogueInput().AppleUrl;
        var result = new PlatformEnrichmentResult(
            Updated: true,
            Service: Service.Apple,
            PlatformUrl: url,
            ReleaseUpdated: true,
            Release: release);
        var context = new EnrichmentContext();

        // Act
        result.ApplyTo(context);

        // Assert
        context.ReleaseUpdated.Should().BeTrue();
        context.AppleUrlUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a platform enrichment result reports release updated but carries no release value, ApplyTo " +
        "does not mark the enrichment context release flag because there is nothing to persist.")]
    public void apply_to_skips_release_when_release_updated_but_value_null()
    {
        // Arrange
        var context = new EnrichmentContext();
        var result = new PlatformEnrichmentResult(
            Updated: true,
            Service: Service.Apple,
            PlatformUrl: _fixture.CreateAppleCatalogueInput().AppleUrl,
            ReleaseUpdated: true,
            Release: null);

        // Act
        result.ApplyTo(context);

        // Assert
        context.ReleaseUpdated.Should().BeFalse();
        context.AppleUrlUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a platform enrichment result carries a service but no platform URL, ApplyTo does not mark " +
        "any platform URL flags because persistence only tracks concrete link updates.")]
    public void apply_to_skips_platform_url_when_url_is_null()
    {
        // Arrange
        var context = new EnrichmentContext();
        var result = new PlatformEnrichmentResult(
            Updated: true,
            Service: Service.Spotify,
            PlatformUrl: null,
            ReleaseUpdated: false,
            Release: null);

        // Act
        result.ApplyTo(context);

        // Assert
        context.SpotifyUrlUpdated.Should().BeFalse();
        context.AppleUrlUpdated.Should().BeFalse();
        context.YouTubeUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a platform enrichment result carries a platform URL but no service, ApplyTo does not mark " +
        "any platform URL flags because the target persistence field is unknown.")]
    public void apply_to_skips_platform_url_when_service_is_null()
    {
        // Arrange
        var context = new EnrichmentContext();
        var result = new PlatformEnrichmentResult(
            Updated: true,
            Service: null,
            PlatformUrl: _fixture.CreateSpotifyCatalogueInput().SpotifyUrl,
            ReleaseUpdated: false,
            Release: null);

        // Act
        result.ApplyTo(context);

        // Assert
        context.SpotifyUrlUpdated.Should().BeFalse();
        context.AppleUrlUpdated.Should().BeFalse();
        context.YouTubeUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a platform enrichment result did not update anything, ApplyTo leaves the enrichment context " +
        "unchanged because no persistence fields were touched.")]
    public void apply_to_leaves_context_unchanged_for_none_result()
    {
        // Arrange
        var context = new EnrichmentContext();

        // Act
        PlatformEnrichmentResult.None.ApplyTo(context);

        // Assert
        context.Updated.Should().BeFalse();
    }

    private Uri CreatePlatformUrl(Service service) =>
        service switch
        {
            Service.Spotify => _fixture.CreateSpotifyCatalogueInput().SpotifyUrl,
            Service.Apple => _fixture.CreateAppleCatalogueInput().AppleUrl,
            Service.YouTube => _fixture.CreateYouTubeCatalogueInput().YouTubeUrl,
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
        };
}
