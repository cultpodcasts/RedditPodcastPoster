using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class IndexingContextExtensionsTests
{
    [Fact]
    public void ForPodcastUpdate_InheritsBatchGlobalSpotifyBypass()
    {
        var batchContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipSpotifyUrlResolving: true);

        batchContext.ForPodcastUpdate().SkipSpotifyUrlResolving.Should().BeTrue();
    }

    [Fact]
    public void AbsorbPodcastPass_PropagatesSpotifyBypassToBatchImmediately()
    {
        var batchContext = new IndexingContext(DateTime.UtcNow.AddDays(-2));
        var podcastContext = batchContext.ForPodcastUpdate();
        podcastContext.SkipSpotifyUrlResolving = true;

        var state = new IndexingContextExtensions.PodcastBatchBypassState(false, false);
        state = batchContext.AbsorbPodcastPass(podcastContext, state);

        batchContext.SkipSpotifyUrlResolving.Should().BeTrue();
        state.AnySpotifyBypassed.Should().BeTrue();
    }

    [Fact]
    public void AbsorbPodcastPass_DoesNotRollUpYouTubeBypassUntilBatchEnd()
    {
        var batchContext = new IndexingContext(DateTime.UtcNow.AddDays(-2));
        var podcastContext = batchContext.ForPodcastUpdate();
        podcastContext.SkipYouTubeUrlResolving = true;

        var state = new IndexingContextExtensions.PodcastBatchBypassState(false, false);
        state = batchContext.AbsorbPodcastPass(podcastContext, state);

        batchContext.SkipYouTubeUrlResolving.Should().BeFalse();
        state.AnyYouTubeBypassed.Should().BeTrue();

        batchContext.ApplyBatchBypassRollup(state);
        batchContext.SkipYouTubeUrlResolving.Should().BeTrue();
    }

    [Fact]
    public void ApplyBatchBypassRollup_DoesNotReapplySpotifyBypass()
    {
        var batchContext = new IndexingContext(DateTime.UtcNow.AddDays(-2));
        var podcastContext = batchContext.ForPodcastUpdate();
        podcastContext.SkipSpotifyUrlResolving = true;

        var state = new IndexingContextExtensions.PodcastBatchBypassState(false, false);
        state = batchContext.AbsorbPodcastPass(podcastContext, state);

        batchContext.SkipSpotifyUrlResolving.Should().BeTrue();
        batchContext.ApplyBatchBypassRollup(state);
        batchContext.SkipSpotifyUrlResolving.Should().BeTrue();
    }

    [Fact]
    public void RunExpensiveYouTubePlaylistPagination_WhenSkipExpensive_ReturnsFalse()
    {
        var podcast = new Podcast { YouTubePlaylistQueryIsExpensive = true };
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipExpensiveYouTubeQueries: true);

        indexingContext.RunExpensiveYouTubePlaylistPagination(podcast).Should().BeFalse();
    }

    [Fact]
    public void RunExpensiveYouTubePlaylistPagination_WhenExpensiveAllowed_ReturnsTrue()
    {
        var podcast = new Podcast { YouTubePlaylistQueryIsExpensive = true };
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipExpensiveYouTubeQueries: false);

        indexingContext.RunExpensiveYouTubePlaylistPagination(podcast).Should().BeTrue();
    }

    [Fact]
    public void RunExpensiveYouTubePlaylistPagination_WhenNotKnownExpensive_ReturnsFalse()
    {
        var podcast = new Podcast();
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipExpensiveYouTubeQueries: false);

        indexingContext.RunExpensiveYouTubePlaylistPagination(podcast).Should().BeFalse();
    }
}
