using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Models;
using Indexer.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Characterises Poster activity enable/disable gates before the function-based Durable migration.
/// </summary>
public class PosterActivityGateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PosterActivityGateRules()
    {
        _mocker.Use(Options.Create(new PosterOptions { ReleasedDaysAgo = 2 }));
        _mocker.Use(Options.Create(new PostingCriteria
        {
            MinimumDuration = TimeSpan.FromMinutes(10),
            TweetDays = 2,
            RedditDays = 2,
            BlueSkyDays = 2,
            CategoriserDays = 2
        }));
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(x => x.Start(It.IsAny<string>()))
            .Returns(_mocker.GetMock<IMemoryProbeScope>().Object);
        var enabledReason = "enabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunPoster(out enabledReason))
            .Returns(true);
        _mocker.GetMock<FunctionContext>()
            .SetupGet(x => x.InvocationId)
            .Returns(_fixture.Create<string>());
    }

    private Task<IndexerContext> Run(IndexerContext indexerContext) =>
        _mocker.CreateInstance<Poster>().Run(
            indexerContext,
            _mocker.GetMock<FunctionContext>().Object);

    [Fact(DisplayName =
        "When RunPoster is false, Poster returns Success without requiring PosterOperationId, because a disabled " +
        "poster must no-op cleanly.")]
    public async Task disabled_poster_returns_success_without_operation_id()
    {
        // Arrange
        var disabledReason = "disabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunPoster(out disabledReason))
            .Returns(false);

        // Act
        var result = await Run(new IndexerContext());

        // Assert
        result.Success.Should().BeTrue();
        result.PosterOperationId.Should().BeNull();
    }

    [Fact(DisplayName =
        "When RunPoster is enabled and PosterOperationId is null, Poster throws ArgumentNullException, because an " +
        "enabled posting pass must carry an operation id.")]
    public async Task enabled_poster_without_operation_id_throws()
    {
        // Arrange
        var indexerContext = new IndexerContext();

        // Act
        var act = () => Run(indexerContext);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName(nameof(IndexerContext.PosterOperationId));
    }
}
