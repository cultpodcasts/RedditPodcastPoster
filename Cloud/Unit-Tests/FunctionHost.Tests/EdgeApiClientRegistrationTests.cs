using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.EdgeApi.Extensions;
using RedditPodcastPoster.EdgeApi.Heroes;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Heroes;
using Xunit;

namespace FunctionHost.Tests;

public class EdgeApiClientRegistrationTests
{
    [Fact(DisplayName =
        "AddEdgeApiClient after AddPodcastServices: last IHeroEpisodePromoter registration is EdgeHeroEpisodePromoter, because Index/SubmitUrl consoles and Cloud hosts must override the null promoter to notify the hero DO.")]
    public void add_edge_api_client_registers_edge_hero_episode_promoter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddPodcastServices();

        // Act
        services.AddEdgeApiClient(bypassCertificateValidation: false);

        // Assert
        services.Last(d => d.ServiceType == typeof(IHeroEpisodePromoter))
            .ImplementationType.Should().Be(typeof(EdgeHeroEpisodePromoter));
        services.Count(d => d.ServiceType == typeof(IHeroEpisodePromoter) &&
                            d.ImplementationType == typeof(NullHeroEpisodePromoter))
            .Should().Be(1);
    }

    [Fact(DisplayName =
        "Indexer host IoC: IHeroEpisodePromoter resolves as EdgeHeroEpisodePromoter, because hourly indexing must append auto-hero episodes to the edge DO.")]
    public async Task indexer_host_resolves_edge_hero_episode_promoter()
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(global::Indexer.Ioc.ConfigureServices);

        // Act
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var promoter = scope.ServiceProvider.GetRequiredService<IHeroEpisodePromoter>();

        // Assert
        promoter.Should().BeOfType<EdgeHeroEpisodePromoter>();
    }

    [Fact(DisplayName =
        "Api host IoC: IHeroEpisodePromoter resolves as EdgeHeroEpisodePromoter, because submit-url and curation paths must append auto-hero episodes to the edge DO.")]
    public async Task api_host_resolves_edge_hero_episode_promoter()
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(global::Api.Ioc.ConfigureServices);

        // Act
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var promoter = scope.ServiceProvider.GetRequiredService<IHeroEpisodePromoter>();

        // Assert
        promoter.Should().BeOfType<EdgeHeroEpisodePromoter>();
    }
}
