using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Catalogue.Authority;
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

/// <summary>
/// #1415 (submission 872299ca, 21 Nov 2026): each item's Spotify / Apple lookups ran three times before anything was
/// written (language probe, identity probe, ingest), several hitting their 20 s budget. One probe answers both the
/// language and the identity question, and the queued ingest never probes again.
/// </summary>
public class DiscoveryCurationPreflightProbeOnceRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<SubmitOptions> _submits = [];
    private DiscoverySubmitResult _probe = new(DiscoverySubmitResultState.LanguageProbeClear);

    public DiscoveryCurationPreflightProbeOnceRules()
    {
        _mocker.GetMock<ILookupRepository>()
            .Setup(r => r.GetSupportedLanguagesConfig())
            .ReturnsAsync(SupportedLanguagesConfig.CreateDefault());
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.TryClaimHeldForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.UpdateDiscoveryInfoContent())
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse());
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .Callback<DiscoveryResult, IndexingContext, SubmitOptions>((_, _, opts) => _submits.Add(opts))
            .ReturnsAsync((DiscoveryResult _, IndexingContext _, SubmitOptions opts) => opts.LanguageProbe
                ? _probe
                : new DiscoverySubmitResult(DiscoverySubmitResultState.EpisodeAlreadyExists));
    }

    [Fact(DisplayName =
        "The preflight probes each item once: with no language or kind hold, the same probe's identity hold is returned, because the identity question needs no second Spotify / Apple lookup.")]
    public async Task preflight_probes_each_item_once()
    {
        // Arrange
        var (request, resultId) = ArrangeItems(2);
        var hold = new CatalogueIdentityHold("Film", _fixture.Create<string>(),
            [new CatalogueAuthorityCandidate(_fixture.Create<string>(), _fixture.Create<string>(), 2020, "Film", "Tmdb", 1, null)]);
        _probe = new DiscoverySubmitResult(DiscoverySubmitResultState.IdentityDecisionRequired, IdentityHold: hold);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.PreflightAsync(request, CancellationToken.None);

        // Assert
        result!.Status.Should().Be(DiscoveryCurationSubmitStatus.IdentityDecisionRequired);
        result.Outcome!.IdentityDecisionsRequired.Should().HaveCount(2).And
            .Contain(x => x.DiscoveryItemId == resultId);
        _submits.Should().HaveCount(2).And.OnlyContain(o => o.LanguageProbe && !o.PersistToDatabase);
    }

    [Fact(DisplayName =
        "A clear preflight returns null (nothing to decide), and the held apply that follows submits each item once without probing, so each item is looked up twice in all (cheap probe + ingest), not three times.")]
    public async Task clear_preflight_then_ingest_submits_once_more()
    {
        // Arrange
        var (request, _) = ArrangeItems(3);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var preflight = await sut.PreflightAsync(request, CancellationToken.None);
        var applied = await sut.ApplyHeldAsync(request, CancellationToken.None);

        // Assert
        preflight.Should().BeNull();
        applied.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        _submits.Count(o => o.LanguageProbe).Should().Be(3);
        _submits.Count(o => !o.LanguageProbe && o.PersistToDatabase).Should().Be(3);
    }

    private (DiscoverySubmitRequest Request, Guid FirstResultId) ArrangeItems(int count)
    {
        var results = Enumerable.Range(0, count)
            .Select(_ => new DiscoveryResult { Id = _fixture.Create<Guid>(), EpisodeName = _fixture.Create<string>() })
            .ToList();
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync(results.AsEnumerable());
        return (new DiscoverySubmitRequest
        {
            DiscoveryResultsDocumentIds = [_fixture.Create<Guid>()],
            ResultIds = results.Select(x => x.Id).ToArray()
        }, results[0].Id);
    }
}
