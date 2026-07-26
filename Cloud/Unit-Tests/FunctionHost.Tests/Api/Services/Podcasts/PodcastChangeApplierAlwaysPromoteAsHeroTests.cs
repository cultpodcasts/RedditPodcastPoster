using Api.Models;
using Api.Services.Podcasts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastChangeApplierAlwaysPromoteAsHeroTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PodcastChangeApplier _applier = new(NullLogger<PodcastChangeApplier>.Instance);

    [Fact(DisplayName =
        "Podcast change request with alwaysPromoteAsHero true: applier sets the podcast flag, because curators toggle auto-hero without rewriting the hero list.")]
    public void applies_always_promote_as_hero_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = false;
        var request = new PodcastChangeRequest { AlwaysPromoteAsHero = true };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.AlwaysPromoteAsHero.Should().BeTrue();
    }
}
