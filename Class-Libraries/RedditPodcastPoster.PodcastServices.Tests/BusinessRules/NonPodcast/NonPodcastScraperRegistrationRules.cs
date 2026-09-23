using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Extensions;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.NonPodcast;

public class NonPodcastScraperRegistrationRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "AddNonPodcastScrapers registers a catalog-keyed adapter for every non-podcast service except BBC, Internet Archive, " +
        "and submit-retired services (e.g. Hulu), so Api, Indexer, and SubmitUrl cannot forget a scraper when a new provider is added.")]
    public void add_non_podcast_scrapers_registers_every_catalog_keyed_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddNonPodcastScrapers();
        using var provider = services.BuildServiceProvider();
        var expected = Enum.GetValues<StreamingService>()
            .Where(service => service is not StreamingService.BbcSounds and not StreamingService.BbcIplayer and not StreamingService.InternetArchive)
            .Where(service => !StreamingServiceWire.IsSubmitRetired(service))
            .ToArray();

        // Act
        var registered = provider.GetServices<INonPodcastServiceAdapter>()
            .Select(adapter => adapter.ResolveService(new Uri("https://example.com/")))
            .ToArray();

        // Assert
        registered.Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName =
        "AddPodcastServices plus AddNonPodcastScrapers resolves a Channel 4 programme URL to the Channel 4 adapter, " +
        "because hosts compose matcher plugins through the shared registration method.")]
    public void composed_registration_resolves_channel4_submit_url()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddPodcastServices();
        services.AddNonPodcastScrapers();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<INonPodcastServiceAdapterResolver>();
        var url = new Uri($"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = resolver.ForSubmit(url);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ResolveService(new Uri("https://example.com/")).Should().Be(StreamingService.Channel4);
    }
}
