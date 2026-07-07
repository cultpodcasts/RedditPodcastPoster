using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Factories;

namespace RedditPodcastPoster.PodcastServices.Tests.DependencyInjection;

public class EpisodeCatalogueAdapterDependencyInjectionTests
{
    [Fact]
    public void Catalogue_adapters_and_factory_resolve_from_add_episodes_domain()
    {
        var services = new ServiceCollection();
        services.AddEpisodesDomain();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IEpisodeCatalogueAdapter<SpotifyCatalogueInput>>()
            .Should().BeOfType<SpotifyEpisodeAdapter>();
        provider.GetRequiredService<IEpisodeCatalogueAdapter<AppleCatalogueInput>>()
            .Should().BeOfType<AppleEpisodeAdapter>();
        provider.GetRequiredService<IEpisodeCatalogueAdapter<YouTubeCatalogueInput>>()
            .Should().BeOfType<YouTubeEpisodeAdapter>();
        provider.GetRequiredService<IEpisodeFromCandidateFactory>()
            .Should().BeOfType<EpisodeFromCandidateFactory>();
    }
}
