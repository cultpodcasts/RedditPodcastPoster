using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.UrlSubmission.Extensions;

namespace RedditPodcastPoster.UrlSubmission.Tests;

public class UrlSubmissionDependencyInjectionTests
{
    [Fact]
    public void EpisodeEnricher_resolves_when_episodes_domain_registered_explicitly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEpisodesDomain();
        services.AddUrlSubmission();

        using var provider = services.BuildServiceProvider();
        var enricher = provider.GetRequiredService<IEpisodeEnricher>();

        enricher.Should().BeOfType<EpisodeEnricher>();
    }

    [Fact]
    public void EpisodeEnricher_fails_without_episodes_domain()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUrlSubmission();

        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IEpisodeEnricher>();
        act.Should().Throw<InvalidOperationException>();
    }
}
