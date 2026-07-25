using FluentAssertions;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// Arbitrary-order YouTube playlist walks must hard-stop after MaxPages so a mis-tagged
/// channel-scale playlist cannot burn the daily YouTube quota.
/// </summary>
public class ArbitraryYouTubePlaylistWalkRules
{
    [Fact(DisplayName =
        "An arbitrary playlist walk trips its circuit breaker once MaxPages have been fetched and " +
        "another page remains, because continuing would burn quota on a channel-scale playlist.")]
    public void Trips_when_max_pages_fetched_and_next_page_remains()
    {
        // Arrange
        var pagesFetched = ArbitraryYouTubePlaylistWalk.MaxPages;
        var nextPageToken = "next-page-token";

        // Act
        var trip = ArbitraryYouTubePlaylistWalk.ShouldTripCircuitBreaker(pagesFetched, nextPageToken);

        // Assert
        trip.Should().BeTrue();
    }

    [Fact(DisplayName =
        "An arbitrary playlist walk does not trip when MaxPages have been fetched but no next page " +
        "remains, because the walk completed within the quota budget.")]
    public void Does_not_trip_when_walk_completes_exactly_at_max_pages()
    {
        // Arrange
        var pagesFetched = ArbitraryYouTubePlaylistWalk.MaxPages;
        string? nextPageToken = null;

        // Act
        var trip = ArbitraryYouTubePlaylistWalk.ShouldTripCircuitBreaker(pagesFetched, nextPageToken);

        // Assert
        trip.Should().BeFalse();
    }

    [Fact(DisplayName =
        "An arbitrary playlist walk does not trip before MaxPages even when a next page remains, " +
        "because curated show playlists are expected to fit inside the page budget.")]
    public void Does_not_trip_before_max_pages_when_next_page_remains()
    {
        // Arrange
        var pagesFetched = ArbitraryYouTubePlaylistWalk.MaxPages - 1;
        var nextPageToken = "next-page-token";

        // Act
        var trip = ArbitraryYouTubePlaylistWalk.ShouldTripCircuitBreaker(pagesFetched, nextPageToken);

        // Assert
        trip.Should().BeFalse();
    }

    [Fact(DisplayName =
        "The arbitrary-walk circuit-breaker message template starts with the stable prefix so App Insights " +
        "can alert operators on the exact log line when a playlist exceeds the page budget.")]
    public void Circuit_breaker_message_template_starts_with_stable_prefix()
    {
        // Arrange
        // Act
        var template = ArbitraryYouTubePlaylistWalk.CircuitBreakerTrippedMessageTemplate;

        // Assert
        template.Should().StartWith(ArbitraryYouTubePlaylistWalk.CircuitBreakerTrippedMessagePrefix);
        template.Should().Contain("playlist-id=");
        template.Should().Contain("pages-fetched=");
        template.Should().Contain("max-pages=");
    }

    [Fact(DisplayName =
        "Arbitrary walk batch size times MaxPages covers one thousand playlist items because that is " +
        "enough for curated show playlists and deliberately too small for channel-scale uploads feeds.")]
    public void Page_budget_covers_one_thousand_items()
    {
        // Arrange
        // Act
        var itemBudget = ArbitraryYouTubePlaylistWalk.BatchSize * ArbitraryYouTubePlaylistWalk.MaxPages;

        // Assert
        itemBudget.Should().Be(1000);
    }
}
