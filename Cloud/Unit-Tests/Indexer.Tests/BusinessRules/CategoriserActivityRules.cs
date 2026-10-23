using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Services;
using Microsoft.DurableTask;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Subjects.Categorisation;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class CategoriserActivityRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private bool _podcastCategorised;
    private bool _catalogueCategorised;
    private IReadOnlyList<PodcastEpisode>? _seenCandidates;

    public CategoriserActivityRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions()));
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(x => x.Start(It.IsAny<string>()))
            .Returns(_mocker.GetMock<IMemoryProbeScope>().Object);
        var reason = "enabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunCategoriser(out reason))
            .Returns(true);
        _mocker.GetMock<IRecentPodcastEpisodeCategoriser>()
            .Setup(x => x.Categorise(It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .Callback<IReadOnlyList<PodcastEpisode>?>(candidates =>
            {
                _podcastCategorised = true;
                _seenCandidates = candidates;
            })
            .ReturnsAsync([]);
        _mocker.GetMock<IRecentCataloguePlayableCategoriser>()
            .Setup(x => x.Categorise())
            .Callback(() => _catalogueCategorised = true)
            .ReturnsAsync([]);
    }

    private Task<IndexerContext> Run(IndexerContext indexerContext) =>
        _mocker.CreateInstance<global::Indexer.Activities.Categoriser>().RunAsync(
            _mocker.GetMock<TaskActivityContext>().Object,
            indexerContext);

    [Fact(DisplayName =
        "When the categoriser activity runs, then it categorises podcast candidates and catalogue playables, because TV episodes and news reports are not on the podcast candidate list.")]
    public async Task activity_categorises_podcast_candidates_and_catalogue_playables()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var candidates = new[] { new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast)) };
        var indexerContext = new IndexerContext(
            CategoriserOperationId: _fixture.CreateGuid(),
            RecentEpisodeCandidates: candidates);

        // Act
        var result = await Run(indexerContext);

        // Assert
        result.Success.Should().BeTrue();
        _podcastCategorised.Should().BeTrue();
        _seenCandidates.Should().BeSameAs(candidates);
        _catalogueCategorised.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the categoriser activity has no podcast candidates, then catalogue playables are still categorised, because a parent pass can add TV episodes and news reports on their own.")]
    public async Task activity_categorises_catalogue_playables_without_podcast_candidates()
    {
        // Arrange
        var indexerContext = new IndexerContext(CategoriserOperationId: _fixture.CreateGuid());

        // Act
        var result = await Run(indexerContext);

        // Assert
        result.Success.Should().BeTrue();
        _podcastCategorised.Should().BeTrue();
        _seenCandidates.Should().BeNull();
        _catalogueCategorised.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the categoriser activity is disabled, then neither podcast episodes nor catalogue playables are categorised, because the activity switch covers both.")]
    public async Task disabled_activity_categorises_nothing()
    {
        // Arrange
        var reason = "disabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunCategoriser(out reason))
            .Returns(false);

        // Act
        var result = await Run(new IndexerContext());

        // Assert
        result.Success.Should().BeTrue();
        _podcastCategorised.Should().BeFalse();
        _catalogueCategorised.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When catalogue categorisation throws, then the activity fails after podcast categorisation, because a playable subject failure must not look like a successful categoriser run.")]
    public async Task catalogue_failure_fails_the_activity_after_podcast_categorisation()
    {
        // Arrange
        _mocker.GetMock<IRecentCataloguePlayableCategoriser>()
            .Setup(x => x.Categorise())
            .ThrowsAsync(new InvalidOperationException("synthetic catalogue failure"));
        var indexerContext = new IndexerContext(CategoriserOperationId: _fixture.CreateGuid());

        // Act
        var act = () => Run(indexerContext);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _podcastCategorised.Should().BeTrue();
    }
}
