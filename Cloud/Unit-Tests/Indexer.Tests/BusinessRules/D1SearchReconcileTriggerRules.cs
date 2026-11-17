using FluentAssertions;
using Indexer.Triggers;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class D1SearchReconcileTriggerRules
{
    private readonly AutoMocker _mocker = new();
    private readonly D1SearchOptions _options = new();
    private D1SearchReconcileRequest? _request;

    public D1SearchReconcileTriggerRules()
    {
        _mocker.Use<IOptions<D1SearchOptions>>(Options.Create(_options));
        _mocker.GetMock<ID1SearchIndexWriter>().SetupGet(w => w.IsActive).Returns(true);
        _mocker.GetMock<ID1SearchReconciler>()
            .Setup(r => r.Reconcile(It.IsAny<D1SearchReconcileRequest>(), It.IsAny<CancellationToken>()))
            .Callback<D1SearchReconcileRequest, CancellationToken>((r, _) => _request = r)
            .ReturnsAsync([]);
    }

    [Fact(DisplayName =
        "When the weekly reconcile timer fires, then it reconciles every target and writes only the configured write " +
        "targets, never above the drift threshold, because the timer is a dry run until Jon names a target.")]
    public async Task timer_writes_only_configured_targets_within_the_threshold()
    {
        // Arrange
        _options.Reconcile.WriteTargets = ["preview"];
        var sut = _mocker.CreateInstance<D1SearchReconcileTrigger>();

        // Act
        await sut.Reconcile(CancellationToken.None);

        // Assert
        _request.Should().BeEquivalentTo(new D1SearchReconcileRequest(null, ["preview"]));
    }

    [Fact(DisplayName =
        "When no D1 target is configured, then the timer does nothing, because a switched-off sync must cost nothing.")]
    public async Task timer_does_nothing_without_a_target()
    {
        // Arrange
        _mocker.GetMock<ID1SearchIndexWriter>().SetupGet(w => w.IsActive).Returns(false);
        var sut = _mocker.CreateInstance<D1SearchReconcileTrigger>();

        // Act
        await sut.Reconcile(CancellationToken.None);

        // Assert
        _request.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the reconcile throws, then the timer logs it and returns, because a D1 problem must not fail the Indexer.")]
    public async Task timer_never_throws()
    {
        // Arrange
        _mocker.GetMock<ID1SearchReconciler>()
            .Setup(r => r.Reconcile(It.IsAny<D1SearchReconcileRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = _mocker.CreateInstance<D1SearchReconcileTrigger>();

        // Act
        var act = () => sut.Reconcile(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
