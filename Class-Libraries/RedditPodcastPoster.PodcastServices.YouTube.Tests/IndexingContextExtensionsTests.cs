using FluentAssertions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class IndexingContextExtensionsTests
{
    [Fact(DisplayName =
        "A podcast update context inherits the batch's Spotify bypass, because Spotify rate limiting is batch-global.")]
    public void ForPodcastUpdate_InheritsBatchGlobalSpotifyBypass()
    {
        // Arrange
        var batchContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipSpotifyUrlResolving: true);

        // Act
        var updateContext = batchContext.ForPodcastUpdate();

        // Assert
        updateContext.SkipSpotifyUrlResolving.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Absorbing a podcast pass turns Spotify bypass on for the batch immediately, because later podcasts in the same batch must skip Spotify.")]
    public void AbsorbPodcastPass_PropagatesSpotifyBypassToBatchImmediately()
    {
        // Arrange
        var batchContext = new IndexingContext(DateTime.UtcNow.AddDays(-2));
        var podcastContext = batchContext.ForPodcastUpdate();
        podcastContext.SkipSpotifyUrlResolving = true;
        var state = new IndexingContextExtensions.PodcastBatchBypassState(false, false);

        // Act
        state = batchContext.AbsorbPodcastPass(podcastContext, state);

        // Assert
        batchContext.SkipSpotifyUrlResolving.Should().BeTrue();
        state.AnySpotifyBypassed.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Absorbing a podcast pass records YouTube bypass without applying it to the batch until rollup, because YouTube bypass stays per podcast until the batch ends.")]
    public void AbsorbPodcastPass_DoesNotRollUpYouTubeBypassUntilBatchEnd()
    {
        // Arrange
        var batchContext = new IndexingContext(DateTime.UtcNow.AddDays(-2));
        var podcastContext = batchContext.ForPodcastUpdate();
        podcastContext.SkipYouTubeUrlResolving = true;
        var state = new IndexingContextExtensions.PodcastBatchBypassState(false, false);

        // Act
        state = batchContext.AbsorbPodcastPass(podcastContext, state);

        // Assert
        batchContext.SkipYouTubeUrlResolving.Should().BeFalse();
        state.AnyYouTubeBypassed.Should().BeTrue();

        batchContext.ApplyBatchBypassRollup(state);
        batchContext.SkipYouTubeUrlResolving.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Batch rollup does not clear a Spotify bypass that a podcast pass already applied, because that bypass is already on the batch parent.")]
    public void ApplyBatchBypassRollup_DoesNotReapplySpotifyBypass()
    {
        // Arrange
        var batchContext = new IndexingContext(DateTime.UtcNow.AddDays(-2));
        var podcastContext = batchContext.ForPodcastUpdate();
        podcastContext.SkipSpotifyUrlResolving = true;
        var state = new IndexingContextExtensions.PodcastBatchBypassState(false, false);
        state = batchContext.AbsorbPodcastPass(podcastContext, state);

        // Act
        batchContext.ApplyBatchBypassRollup(state);

        // Assert
        batchContext.SkipSpotifyUrlResolving.Should().BeTrue();
    }
}
