using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Reddit.Factories;

namespace RedditPodcastPoster.Reddit.Tests;

public class RedditServicesDependencyInjectionTests
{
    [Fact(DisplayName =
        "AddRedditServices registration: when called by a future Devvit host, then title and comment constructors are registered without a live poster port, because application hosts no longer call AddRedditServices.")]
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
