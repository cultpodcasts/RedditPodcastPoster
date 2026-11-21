using FluentAssertions;
using Api.Services.Catalogue;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

/// <summary>The film / TV episode / news report <c>services</c> PATCH with a stored <c>"key": null</c> entry (#1508).</summary>
public class PlayableServicesChangeApplierRules
{
    private static readonly Uri SpotifyUrl = new("https://open.spotify.com/episode/syntheticSpotifyEpisode01");
    private static readonly Uri YouTubeUrl = new("https://www.youtube.com/watch?v=synthetic01");
    private static readonly Uri OtherYouTubeUrl = new("https://www.youtube.com/watch?v=synthetic02");

    private static Dictionary<string, ServiceLink> StoredWithNullSpotify() =>
        new(StringComparer.Ordinal)
        {
            ["spotify"] = null!,
            ["youtube"] = new ServiceLink { Url = YouTubeUrl }
        };

    [Fact(DisplayName =
        "Plain English rule: when the stored services map has a null entry and the curator adds a url for that same service, then the patch applies without throwing and the service gets the new link, because the curator DTO hides the null key so adding it is the expected edit.")]
    public void apply_adds_link_on_null_entry_key()
    {
        // Arrange
        var current = StoredWithNullSpotify();
        var patch = new Dictionary<string, ServiceLink> { ["spotify"] = new() { Url = SpotifyUrl } };

        // Act
        var result = PlayableServicesChangeApplier.Apply(current, patch);

        // Assert
        result.Should().NotBeNull();
        result!["spotify"].Should().NotBeNull();
        result["spotify"].Url.Should().Be(SpotifyUrl);
        result["youtube"].Url.Should().Be(YouTubeUrl);
    }

    [Fact(DisplayName =
        "Plain English rule: when the stored services map has a null entry and the curator patches a different service, then the patch applies and the null entry is dropped, because any services PATCH should heal the stored document.")]
    public void apply_on_other_key_drops_null_entry()
    {
        // Arrange
        var current = StoredWithNullSpotify();
        var patch = new Dictionary<string, ServiceLink> { ["youtube"] = new() { Url = OtherYouTubeUrl } };

        // Act
        var result = PlayableServicesChangeApplier.Apply(current, patch);

        // Assert
        result.Should().NotBeNull();
        result!.Should().NotContainKey("spotify");
        result["youtube"].Url.Should().Be(OtherYouTubeUrl);
    }

    [Fact(DisplayName =
        "Plain English rule: when a services patch is applied, then the loaded entity's existing link instances are not mutated, because the applier returns a new map and the caller decides whether to save it.")]
    public void apply_does_not_mutate_current_links()
    {
        // Arrange
        var current = StoredWithNullSpotify();
        var storedYouTube = current["youtube"];
        var patch = new Dictionary<string, ServiceLink> { ["youtube"] = new() { Url = OtherYouTubeUrl } };

        // Act
        PlayableServicesChangeApplier.Apply(current, patch);

        // Assert
        storedYouTube.Url.Should().Be(YouTubeUrl);
        current.Should().ContainKey("spotify");
    }
}
