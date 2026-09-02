using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.People;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Extensions;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace RedditPodcastPoster.UrlSubmission.Tests;

public class UrlSubmissionDependencyInjectionTests
{
    [Fact(DisplayName =
        "AddUrlSubmission resolves IEpisodeEnricher when episodes domain is registered at the composition root.")]
    public void EpisodeEnricher_resolves_when_episodes_domain_registered_explicitly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEpisodesDomain();
        services.AddUrlSubmission();

        // Act
        using var provider = services.BuildServiceProvider();
        var enricher = provider.GetRequiredService<IEpisodeEnricher>();

        // Assert
        enricher.Should().BeOfType<EpisodeEnricher>();
    }

    [Fact(DisplayName =
        "AddUrlSubmission does not resolve IEpisodeEnricher without episodes domain, because callers must register it at the root.")]
    public void EpisodeEnricher_fails_without_episodes_domain()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUrlSubmission();
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => provider.GetRequiredService<IEpisodeEnricher>();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName =
        "AddUrlSubmission registers URL membership lookup so submit GET can classify stored episode URLs without ingest.")]
    public void AddUrlSubmission_registers_url_membership_lookup()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddUrlSubmission();

        // Assert
        services.Should().Contain(d => d.ServiceType == typeof(IUrlMembershipLookup));
        services.Should().Contain(d => d.ServiceType == typeof(IUrlSubmitter));
    }
}
