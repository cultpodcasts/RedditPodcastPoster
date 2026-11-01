using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Extensions;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.Registration;

/// <summary>
/// Parent routing is not optional: every host that registers URL submission gets <see cref="UrlSubmitter"/> with the
/// news organisation / TV show lookup and submitter, so a URL from an owned channel can never fall through to a podcast.
/// </summary>
public class UrlSubmitterRegistrationRules
{
    /// <summary>AddUrlSubmission with its outside collaborators (repositories, podcast service) mocked.</summary>
    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddUrlSubmission();
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IPodcastService>()));
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IUrlCategoriser>()));
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<ICategorisedItemProcessor>()));
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IPodcastProcessor>()));
        services.AddSingleton(Mock.Of<IPodcastRepository>());
        services.AddSingleton(Mock.Of<INewsOrganisationRepository>());
        services.AddSingleton(Mock.Of<ITvShowRepository>());
        services.AddSingleton(Mock.Of<INewsReportRepository>());
        services.AddSingleton(Mock.Of<ITvShowEpisodeRepository>());
        // UrlSubmitter checks film, TV episode and news report ownership of a podcast-platform URL (#1089).
        services.AddSingleton(Mock.Of<IEpisodeRepository>());
        services.AddSingleton(Mock.Of<IFilmRepository>());
        // Hosts get this from AddPodcastServices.
        services.AddSingleton(Mock.Of<INonPodcastServiceAdapterResolver>());
        // Hosts get this from AddPodcastServices (Null) or AddEdgeApiClient; the podcast and parent submitters both take it.
        services.AddSingleton(Mock.Of<IHeroEpisodePromoter>());
        return services;
    }

    [Fact(DisplayName =
        "AddUrlSubmission resolves IUrlSubmitter with the catalogue parent lookup and submitter, because routing to news organisations and TV shows is always on.")]
    public void url_submitter_resolves_with_parent_routing()
    {
        // Arrange
        using var provider = Services().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        // Act
        var submitter = scope.ServiceProvider.GetRequiredService<IUrlSubmitter>();

        // Assert
        submitter.Should().BeOfType<UrlSubmitter>();
        scope.ServiceProvider.GetRequiredService<ICatalogueParentLookup>().Should().BeOfType<CatalogueParentLookup>();
        scope.ServiceProvider.GetRequiredService<ICatalogueParentSubmitter>().Should()
            .BeOfType<CatalogueParentSubmitter>();
    }

    [Fact(DisplayName =
        "AddUrlSubmission resolves IDiscoveryUrlSubmitter as the catalogue ownership decorator over DiscoveryUrlSubmitter, so curator submit and supervisor apply never re-create a URL a film, TV episode or news report owns.")]
    public void discovery_url_submitter_resolves_behind_the_ownership_guard()
    {
        // Arrange
        var services = Services();
        // Its categorisers need podcast-service clients the hosts register; this rule is about the wrapper.
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IDiscoveryResultProcessor>()));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        // Act
        var submitter = scope.ServiceProvider.GetRequiredService<IDiscoveryUrlSubmitter>();

        // Assert
        submitter.Should().BeOfType<CatalogueOwnedDiscoveryUrlSubmitter>();
        scope.ServiceProvider.GetRequiredService<ICatalogueOwnershipGuard>().Should()
            .BeOfType<CatalogueOwnershipGuard>();
    }

    [Fact(DisplayName =
        "Without a catalogue parent lookup, IUrlSubmitter does not resolve, because the routing collaborators are required constructor parameters.")]
    public void url_submitter_requires_parent_lookup()
    {
        // Arrange
        var services = Services();
        services.RemoveAll<ICatalogueParentLookup>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var resolve = () => scope.ServiceProvider.GetRequiredService<IUrlSubmitter>();

        // Assert
        resolve.Should().Throw<InvalidOperationException>().WithMessage("*ICatalogueParentLookup*");
    }
}
