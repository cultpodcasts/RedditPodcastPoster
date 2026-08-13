using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Reddit.Factories;

namespace RedditPodcastPoster.Reddit.Tests;

public class RedditServicesDependencyInjectionTests
{
    [Fact(DisplayName =
        "AddRedditServices registration: when called, then title and comment constructors are registered for a future Devvit host.")]
    public void add_reddit_services_registers_constructors()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRedditServices();

        // Assert
        services.Should().Contain(d =>
            d.ServiceType == typeof(IRedditPostTitleFactory) &&
            d.ImplementationType == typeof(RedditPostTitleFactory));
        services.Should().Contain(d =>
            d.ServiceType == typeof(IRedditEpisodeCommentFactory) &&
            d.ImplementationType == typeof(RedditEpisodeCommentFactory));
        services.Should().Contain(d =>
            d.ServiceType == typeof(IRedditBundleCommentFactory) &&
            d.ImplementationType == typeof(RedditBundleCommentFactory));
    }
}
