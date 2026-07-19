using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Persistence.Abstractions;
using Xunit;

namespace Discovery.Tests;

public class DiscoveryLookbackResolverBusinessRulesTests
{
    private static readonly TimeSpan Overlap10m = TimeSpan.FromMinutes(10);

    [Fact(DisplayName =
        "Fail-closed: repository returns null (no prior success) → throws DiscoveryLookbackUnavailableException.")]
    public async Task Failure_repository_null_fail_closed()
    {
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        var sut = CreateSut(Overlap10m, repo.Object);

        var act = async () => await sut.ResolveAsync();

        var ex = await act.Should().ThrowAsync<DiscoveryLookbackUnavailableException>();
        ex.Which.Message.Should().Contain("fail-closed");
        ex.Which.Message.Should().Contain("CLI");
        repo.Verify(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Fail-closed: repository throws → wraps as DiscoveryLookbackUnavailableException (no static fallback).")]
    public async Task Failure_repository_throws_fail_closed()
    {
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));
        var sut = CreateSut(Overlap10m, repo.Object);

        var act = async () => await sut.ResolveAsync();

        var ex = await act.Should().ThrowAsync<DiscoveryLookbackUnavailableException>();
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
        ex.Which.Message.Should().Contain("fail-closed");
    }

    [Fact(DisplayName =
        "Dynamic + prior success → since = lastSuccess - 10m.")]
    public async Task Dynamic_with_prior_success_anchors_to_lastSuccess_minus_overlap()
    {
        var lastSuccess = DateTime.UtcNow.AddMinutes(-12).ToUniversalTime();
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastSuccess);
        var sut = CreateSut(Overlap10m, repo.Object);

        var result = await sut.ResolveAsync();

        result.LatestSuccessfulDiscoveryBegan.Should().Be(lastSuccess);
        result.Since.Should().Be(lastSuccess.Subtract(Overlap10m));
    }

    [Fact(DisplayName =
        "Recent catch-up watermark → since = catch-up discoveryBegan - 10m.")]
    public async Task Failure_recent_catchup_watermark_uses_overlap_only()
    {
        var catchUpBegan = DateTime.UtcNow.AddMinutes(-8).ToUniversalTime();
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ReturnsAsync(catchUpBegan);
        var sut = CreateSut(Overlap10m, repo.Object);

        var result = await sut.ResolveAsync();

        result.Since.Should().Be(catchUpBegan.Subtract(Overlap10m));
        (DateTime.UtcNow - result.Since).Should().BeLessThan(TimeSpan.FromMinutes(25));
    }

    private static DiscoveryLookbackResolver CreateSut(
        TimeSpan overlap,
        IDiscoveryResultsRepository repository)
    {
        var options = Options.Create(new DiscoverOptions
        {
            DynamicLookbackOverlap = overlap
        });
        return new DiscoveryLookbackResolver(
            options,
            repository,
            NullLogger<DiscoveryLookbackResolver>.Instance);
    }
}
