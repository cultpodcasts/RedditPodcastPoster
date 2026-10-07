using FluentAssertions;
using idunno.AtProto;
using idunno.Bluesky;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.RichText;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class BlueskyAgentOptionsFactoryRules
{
    [Fact(DisplayName =
        "Bluesky agent options resolve a dashed @mention, because the options factory installs the dashed-handle facet extractor.")]
    public async Task options_resolve_a_dashed_mention()
    {
        // Arrange
        const string handle = "some-name.bsky.social";
        string? resolved = null;
        var sut = new BlueskyAgentOptionsFactory();
        var options = sut.Create((candidate, _) =>
        {
            resolved = candidate;
            return Task.FromResult<Did?>(null);
        });

        // Act
        await options.FacetExtractor!.ExtractFacets($"é @{handle}");

        // Assert
        resolved.Should().Be(handle);
    }

    [Fact(DisplayName =
        "Bluesky service registration supplies agent options to the agent factory, because posts use the dashed-mention extractor from the container.")]
    public void service_registration_supplies_agent_options()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBlueskyServices();

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BlueskyAgentOptions>();
        var agentFactory = provider.GetRequiredService<IBlueskyAgentFactory>();

        // Assert
        options.FacetExtractor.Should().BeOfType<DashedHandleFacetExtractor>();
        agentFactory.Should().BeOfType<BlueskyAgentFactory>();
    }
}
