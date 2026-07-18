using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Persistence.Abstractions;
using Xunit;

namespace Discovery.Tests;

/// <summary>
/// Resolver-level business rules: Cosmos watermark lookup, Static vs Dynamic mode selection,
/// and fallback when the repository returns null or throws.
/// </summary>
public class DiscoveryLookbackResolverBusinessRulesTests
{
    private static readonly TimeSpan SearchSince = TimeSpan.Parse("6:10:00");
    private static readonly TimeSpan Overlap10m = TimeSpan.FromMinutes(10);

    [Fact(DisplayName =
        "Failure scenario: repository returns null (no prior success) → ModeUsed=Static, " +
        "since ≈ UtcNow - SearchSince.")]
    public async Task Failure_repository_null_falls_back_to_static_mode()
    {
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        var sut = CreateSut(DiscoveryLookbackMode.Dynamic, Overlap10m, repo.Object);
        var before = DateTime.UtcNow;

        var result = await sut.ResolveAsync();

        var after = DateTime.UtcNow;
        result.ModeUsed.Should().Be(DiscoveryLookbackMode.Static);
        result.LatestSuccessfulDiscoveryBegan.Should().BeNull();
        result.Since.Should().BeOnOrAfter(before.Subtract(SearchSince).AddSeconds(-1));
        result.Since.Should().BeOnOrBefore(after.Subtract(SearchSince).AddSeconds(1));
        repo.Verify(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Failure scenario: repository throws → catch and fall back to static SearchSince window " +
        "(ModeUsed=Static, no watermark).")]
    public async Task Failure_repository_throws_falls_back_to_static_mode()
    {
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));
        var sut = CreateSut(DiscoveryLookbackMode.Dynamic, Overlap10m, repo.Object);
        var before = DateTime.UtcNow;

        var result = await sut.ResolveAsync();

        var after = DateTime.UtcNow;
        result.ModeUsed.Should().Be(DiscoveryLookbackMode.Static);
        result.LatestSuccessfulDiscoveryBegan.Should().BeNull();
        result.Since.Should().BeOnOrAfter(before.Subtract(SearchSince).AddSeconds(-1));
        result.Since.Should().BeOnOrBefore(after.Subtract(SearchSince).AddSeconds(1));
    }

    [Fact(DisplayName =
        "Business rule: LookbackMode=Static never queries Cosmos and ignores any prior success watermark.")]
    public async Task Static_mode_does_not_query_repository()
    {
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        var sut = CreateSut(DiscoveryLookbackMode.Static, Overlap10m, repo.Object);
        var before = DateTime.UtcNow;

        var result = await sut.ResolveAsync();

        var after = DateTime.UtcNow;
        result.ModeUsed.Should().Be(DiscoveryLookbackMode.Static);
        result.LatestSuccessfulDiscoveryBegan.Should().BeNull();
        result.Since.Should().BeOnOrAfter(before.Subtract(SearchSince).AddSeconds(-1));
        result.Since.Should().BeOnOrBefore(after.Subtract(SearchSince).AddSeconds(1));
        repo.Verify(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Business rule: Dynamic + prior success → ModeUsed=Dynamic, since = lastSuccess - 10m, " +
        "not a full SearchSince re-search.")]
    public async Task Dynamic_with_prior_success_anchors_to_lastSuccess_minus_overlap()
    {
        var lastSuccess = DateTime.UtcNow.AddMinutes(-12).ToUniversalTime();
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastSuccess);
        var sut = CreateSut(DiscoveryLookbackMode.Dynamic, Overlap10m, repo.Object);

        var result = await sut.ResolveAsync();

        result.ModeUsed.Should().Be(DiscoveryLookbackMode.Dynamic);
        result.LatestSuccessfulDiscoveryBegan.Should().Be(lastSuccess);
        result.Since.Should().Be(lastSuccess.Subtract(Overlap10m));
        (DateTime.UtcNow - result.Since).Should().BeLessThan(SearchSince);
    }

    [Fact(DisplayName =
        "Failure scenario: catch-up then recycle minutes later via resolver → since = catch-up " +
        "discoveryBegan - 10m; window far shorter than SearchSince.")]
    public async Task Failure_recent_catchup_watermark_does_not_reopen_SearchSince()
    {
        var catchUpBegan = DateTime.UtcNow.AddMinutes(-8).ToUniversalTime();
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ReturnsAsync(catchUpBegan);
        var sut = CreateSut(DiscoveryLookbackMode.Dynamic, Overlap10m, repo.Object);

        var result = await sut.ResolveAsync();

        result.ModeUsed.Should().Be(DiscoveryLookbackMode.Dynamic);
        result.Since.Should().Be(catchUpBegan.Subtract(Overlap10m));
        (DateTime.UtcNow - result.Since).Should().BeLessThan(TimeSpan.FromMinutes(25));
        (DateTime.UtcNow - result.Since).Should().BeLessThan(SearchSince);
    }

    private static DiscoveryLookbackResolver CreateSut(
        DiscoveryLookbackMode mode,
        TimeSpan overlap,
        IDiscoveryResultsRepository repository)
    {
        var options = Options.Create(new DiscoverOptions
        {
            SearchSince = "6:10:00",
            LookbackMode = mode,
            DynamicLookbackOverlap = overlap
        });
        return new DiscoveryLookbackResolver(
            options,
            repository,
            NullLogger<DiscoveryLookbackResolver>.Instance);
    }
}
