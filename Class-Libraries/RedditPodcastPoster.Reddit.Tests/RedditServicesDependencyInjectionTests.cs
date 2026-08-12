using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Reddit.Factories;

namespace RedditPodcastPoster.Reddit.Tests;

public class RedditServicesDependencyInjectionTests
{
    [Fact(DisplayName =
        "AddRedditServices registration: when called, then title and comment constructors are registered and no live poster port is wired, because Reddit.NET posting is detached pending Devvit.")]
    public void add_reddit_services_registers_constructors_without_live_poster()
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
        services.Should().NotContain(d =>
            d.ServiceType.Name == "IEpisodePostManager" ||
            d.ServiceType.Name == "IPostManager");
    }
}
