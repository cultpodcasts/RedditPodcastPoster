using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Reddit.Episodes;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.SocialPosting.Episodes;

namespace RedditPodcastPoster.Reddit.Tests;

public class RedditServicesDependencyInjectionTests
{
    [Fact(DisplayName =
        "AddRedditServices registration: when called, then IEpisodePostManager maps to EpisodePostManager, because Reddit owns the SocialPosting posting port implementation.")]
    public void add_reddit_services_registers_episode_post_manager()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRedditServices();

        // Assert
        services.Should().Contain(d =>
            d.ServiceType == typeof(IEpisodePostManager) &&
            d.ImplementationType == typeof(EpisodePostManager));
    }
}
