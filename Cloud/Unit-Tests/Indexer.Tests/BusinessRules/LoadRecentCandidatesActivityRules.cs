using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.SocialPosting.Episodes;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Characterises LoadRecentCandidates short-circuit and provider load before the function-based Durable migration.
/// </summary>
public class LoadRecentCandidatesActivityRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IList<PodcastEpisode> _loadedCandidates = [];

    public LoadRecentCandidatesActivityRules()
    {
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
        _mocker.GetMock<IRecentEpisodeCandidatesProvider>()
            .Setup(x => x.GetRecentActiveEpisodes(It.IsAny<DateTime>()))
            .ReturnsAsync(() => _loadedCandidates);
        _mocker.GetMock<FunctionContext>()
            .SetupGet(x => x.InvocationId)
            .Returns(_fixture.Create<string>());
    }

    private Task<IndexerContext> Run(IndexerContext indexerContext) =>
        _mocker.CreateInstance<LoadRecentCandidates>().Run(
            indexerContext,
            _mocker.GetMock<FunctionContext>().Object);

    [Fact(DisplayName =
        "When RecentEpisodeCandidates is already set, LoadRecentCandidates returns the context unchanged without " +
        "calling the provider, because preloaded candidates must not be re-queried.")]
    public async Task preloaded_candidates_skip_provider()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var candidates = new[] { new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast)) };
        var indexerContext = new IndexerContext(RecentEpisodeCandidates: candidates);

        // Act
        var result = await Run(indexerContext);

        // Assert
        result.Should().BeSameAs(indexerContext);
        result.RecentEpisodeCandidates.Should().BeSameAs(candidates);
        _mocker.GetMock<IRecentEpisodeCandidatesProvider>().Verify(
            x => x.GetRecentActiveEpisodes(It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When RecentEpisodeCandidates is null, LoadRecentCandidates loads via IRecentEpisodeCandidatesProvider and " +
        "sets the array on the returned context.")]
    public async Task null_candidates_loads_via_provider()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _loadedCandidates =
        [
            new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast))
        ];

        // Act
        var result = await Run(new IndexerContext());

        // Assert
        result.RecentEpisodeCandidates.Should().Equal(_loadedCandidates);
        _mocker.GetMock<IRecentEpisodeCandidatesProvider>().Verify(
            x => x.GetRecentActiveEpisodes(It.IsAny<DateTime>()),
            Times.Once);
    }
}
