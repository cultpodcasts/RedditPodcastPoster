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
            .Setup(r => r.Reconcile(It.IsAny<D1SearchReconcileRequest>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<D1SearchReconcileProgress>>()))
            .Callback<D1SearchReconcileRequest, CancellationToken, IProgress<D1SearchReconcileProgress>?>((r, _, _) => _request = r)
            .ReturnsAsync(new D1SearchReconcileReport(D1SearchReconcileOutcome.InSync, 0, 0, 0, 0, 0, 0, 0));
    }

    [Theory(DisplayName =
        "When the weekly reconcile timer fires, then it writes only when d1Search:Reconcile:Write is set, never above " +
        "the drift threshold, because the timer is a dry run until Jon turns writing on.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task timer_writes_only_when_configured_within_the_threshold(bool write)
    {
        // Arrange
        _options.Reconcile.Write = write;
        var sut = _mocker.CreateInstance<D1SearchReconcileTrigger>();

        // Act
        await sut.Reconcile(CancellationToken.None);

        // Assert
        _request.Should().Be(new D1SearchReconcileRequest(write, Full: true));
    }

    [Fact(DisplayName =
        "When the D1 sync is off, then the timer does nothing, because a switched-off sync must cost nothing.")]
    public async Task timer_does_nothing_when_the_sync_is_off()
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
            .Setup(r => r.Reconcile(It.IsAny<D1SearchReconcileRequest>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<D1SearchReconcileProgress>>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = _mocker.CreateInstance<D1SearchReconcileTrigger>();

        // Act
        var act = () => sut.Reconcile(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
