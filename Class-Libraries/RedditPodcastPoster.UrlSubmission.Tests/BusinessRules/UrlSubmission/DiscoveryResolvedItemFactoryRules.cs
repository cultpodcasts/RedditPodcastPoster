using FluentAssertions;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class DiscoveryResolvedItemFactoryRules
{
    [Fact(DisplayName =
        "From builds a stand-in with no streaming service or canonical URL, and author-as-series naming is not applied, because Spotify, Apple and YouTube must not be labelled as Vimeo.")]
    public void stand_in_has_no_service_and_does_not_use_vimeo_author_naming()
    {
        // Arrange
        var url = new Uri("https://www.youtube.com/watch?v=abcdefghijk");
        var discovery = new DiscoveryResult
        {
            Id = Guid.NewGuid(),
            EpisodeName = "A discovered film title",
            ShowName = "A Spotify show",
            Urls = new DiscoveryResultUrls { YouTube = url }
        };
        var categorised = new CategorisedItem(
            null, null, null, null, null,
            new CategorisedYouTubeItem(
                "UCchan", "abcdefghijk", "A Spotify show", "", "A Spotify show",
                "A discovered film title", "", DateTime.UtcNow, TimeSpan.FromMinutes(90), url, false, null, null),
            null, Service.YouTube);

        // Act
        var item = DiscoveryResolvedItemFactory.From(discovery, categorised);

        // Assert
        item.StreamingService.Should().BeNull();
        item.Url.Should().BeNull();
        item.Title.Should().Be("A discovered film title");
        item.ShowName.Should().Be("A Spotify show");
        item.Publisher.Should().Be("A Spotify show");
        NonPodcastShowNameResolver.TrySeriesName(item.ShowName, item.Publisher, item.StreamingService)
            .Should().BeNull();
        NonPodcastShowNameResolver.TrySeriesName(item.ShowName, item.Publisher, StreamingService.Vimeo)
            .Should().Be("A Spotify show");
    }
}
