using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryCurationPreflightExpensiveSkipRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IndexingContext? _preflightContext;

    public DiscoveryCurationPreflightExpensiveSkipRules()
    {
        _mocker.GetMock<ILookupRepository>()
            .Setup(r => r.GetSupportedLanguagesConfig())
            .ReturnsAsync(SupportedLanguagesConfig.CreateDefault());
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.TryClaimForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.UpdateDiscoveryInfoContent())
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse());
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .Callback<DiscoveryResult, IndexingContext, SubmitOptions>((_, ctx, opts) =>
            {
                if (opts.LanguageProbe)
                {
                    _preflightContext = ctx;
                }
            })
            .ReturnsAsync(new DiscoverySubmitResult(DiscoverySubmitResultState.NoUrls));
    }

    [Fact(DisplayName =
        "Language preflight skips expensive Spotify and YouTube lookups so the probe pass does not double the wall-clock of ingest and blow edge timeouts before the Save response.")]
    public async Task preflight_skips_expensive_lookups()
    {
        // Arrange
        var result = new DiscoveryResult { Id = _fixture.Create<Guid>(), EpisodeName = _fixture.Create<string>() };
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync([result]);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        await sut.SubmitAsync(
            new DiscoverySubmitRequest
            {
                DiscoveryResultsDocumentIds = [_fixture.Create<Guid>()],
                ResultIds = [result.Id]
            },
            CancellationToken.None);

        // Assert
        _preflightContext.Should().NotBeNull();
        _preflightContext!.SkipExpensiveSpotifyQueries.Should().BeTrue();
        _preflightContext.SkipExpensiveYouTubeQueries.Should().BeTrue();
    }
}
