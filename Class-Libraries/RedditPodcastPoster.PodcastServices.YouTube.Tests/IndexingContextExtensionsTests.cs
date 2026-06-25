using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class IndexingContextExtensionsTests
{
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
