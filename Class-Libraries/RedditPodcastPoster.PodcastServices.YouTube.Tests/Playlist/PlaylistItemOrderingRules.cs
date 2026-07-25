using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// Head-order probe for YouTube playlists: newest-first enables ReleasedSince early-stop;
/// any strict ascending pair marks the playlist expensive. Equal timestamps are treated as
/// non-ascending — the curated-playlist failure mode that motivates PlaylistOrder.Arbitrary.
/// </summary>
public class PlaylistItemOrderingRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When playlist items are strictly newest-first by added-at, IsReverseDateOrdered returns true " +
        "because reverse-chrono playlists may early-stop once items fall before ReleasedSince.")]
    public void Newest_first_is_reverse_date_ordered()
    {
        // Arrange
        var newer = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(12));
        var older = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(12));
        var items = new[] { Item(newer), Item(older) };

        // Act
        var reverse = PlaylistItemOrdering.IsReverseDateOrdered(items);

        // Assert
        reverse.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When playlist items are strictly oldest-first by added-at, IsReverseDateOrdered returns false " +
        "because ascending playlists need a full walk and must set the expensive-query flag.")]
    public void Oldest_first_is_not_reverse_date_ordered()
    {
        // Arrange
        var older = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(12));
        var newer = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(12));
        var items = new[] { Item(older), Item(newer) };

        // Act
        var reverse = PlaylistItemOrdering.IsReverseDateOrdered(items);

        // Assert
        reverse.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When adjacent playlist items share the same added-at timestamp, IsReverseDateOrdered returns true " +
        "because equal timestamps are non-ascending — the curated bulk-add failure mode that can " +
        "misclassify Arbitrary playlists as newest-first.")]
    public void Equal_added_at_timestamps_count_as_reverse_date_ordered()
    {
        // Arrange — KNOWN: equal timestamps satisfy reverse-chrono; Arbitrary exists because of this
        var shared = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(12));
        var items = new[] { Item(shared), Item(shared), Item(shared) };

        // Act
        var reverse = PlaylistItemOrdering.IsReverseDateOrdered(items);

        // Assert
        reverse.Should().BeTrue(
            "equal added-at pairs never trip current < next; curated bulk-adds look newest-first to the probe");
    }

    [Fact(DisplayName =
        "When a newest-first head is followed by a later-added item deeper in the sample, IsReverseDateOrdered " +
        "returns false at the first ascending pair because one out-of-order item is enough to reject early-stop.")]
    public void First_ascending_pair_rejects_reverse_date_order()
    {
        // Arrange
        var newest = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(12));
        var middle = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(12));
        var appendedNewer = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(6));
        var items = new[] { Item(newest), Item(middle), Item(appendedNewer) };

        // Act
        var reverse = PlaylistItemOrdering.IsReverseDateOrdered(items);

        // Assert
        reverse.Should().BeFalse();
    }

    [Fact(DisplayName =
        "An empty playlist sample is treated as reverse-date-ordered because there is nothing to disprove " +
        "newest-first and the expensive-query flag must not flip on an empty probe.")]
    public void Empty_sample_is_reverse_date_ordered()
    {
        // Arrange
        var items = Array.Empty<PlaylistItem>();

        // Act
        var reverse = PlaylistItemOrdering.IsReverseDateOrdered(items);

        // Assert
        reverse.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A single playlist item is treated as reverse-date-ordered because one sample cannot distinguish " +
        "ascending from newest-first and the expensive-query flag requires MinimumOrderSampleSize.")]
    public void Single_item_is_reverse_date_ordered()
    {
        // Arrange
        var items = new[] { Item(DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(12))) };

        // Act
        var reverse = PlaylistItemOrdering.IsReverseDateOrdered(items);

        // Assert
        reverse.Should().BeTrue();
    }

    private PlaylistItem Item(DateTime publishedAt) =>
        new()
        {
            Snippet = new PlaylistItemSnippet
            {
                Title = _fixture.CreateTitle(),
                PublishedAtDateTimeOffset = new DateTimeOffset(publishedAt, TimeSpan.Zero)
            }
        };
}
