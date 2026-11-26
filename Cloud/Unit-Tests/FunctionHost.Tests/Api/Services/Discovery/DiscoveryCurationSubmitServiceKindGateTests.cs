using System.Linq.Expressions;
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
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryCurationSubmitServiceKindGateTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<SubmitOptions> _persisted = [];
    private readonly Guid _documentId;
    private DiscoveryKindHold? _suggestion;

    public DiscoveryCurationSubmitServiceKindGateTests()
    {
        _documentId = _fixture.Create<Guid>();
        _mocker.GetMock<ILookupRepository>()
            .Setup(r => r.GetSupportedLanguagesConfig())
            .ReturnsAsync(SupportedLanguagesConfig.CreateDefault());
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.TryClaimForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.UpdateDiscoveryInfoContent())
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.MarkAsProcessed(
                It.IsAny<Guid[]>(), It.IsAny<Guid[]>(), It.IsAny<Guid[]>(),
                It.IsAny<IReadOnlyList<DiscoverySubmitItemOutcome>>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(ToAsync(Array.Empty<Podcast>()));
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .Callback<DiscoveryResult, IndexingContext, SubmitOptions>((_, _, opts) =>
            {
                if (opts.PersistToDatabase)
                {
                    _persisted.Add(opts);
                }
            })
            .ReturnsAsync(new DiscoverySubmitResult(DiscoverySubmitResultState.NoUrls));
        _mocker.GetMock<IDiscoveryKindSuggester>()
            .Setup(s => s.SuggestAsync(It.IsAny<DiscoveryResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _suggestion);
    }

    [Fact(DisplayName =
        "A kind hint with no curator decision holds the submit as KindDecisionRequired, Submit is never called with PersistToDatabase, the reports are not claimed, and the results are empty, because nothing auto-applies.")]
    public async Task hint_without_decision_holds_and_ingests_nothing()
    {
        // Arrange
        var result = Result();
        var candidate = Candidate(DiscoveryKindKeys.Film(_fixture.Create<Guid>()));
        _suggestion = new DiscoveryKindHold("film", result.EpisodeName!, [candidate]);
        ArrangeResults(result);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var submitted = await sut.SubmitAsync(Request(result), CancellationToken.None);

        // Assert
        submitted.Status.Should().Be(DiscoveryCurationSubmitStatus.KindDecisionRequired);
        submitted.Outcome!.Results.Should().BeEmpty();
        submitted.Outcome.KindDecisionsRequired.Should().ContainSingle()
            .Which.DiscoveryItemId.Should().Be(result.Id);
        _persisted.Should().BeEmpty();
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.TryClaimForProcessing(
            It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDiscoveryKindSuggester>().Verify(
            s => s.SuggestAsync(It.Is<DiscoveryResult>(r => r.Id == result.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "A continue decision submits the item with no ClassificationSignals so it stays a podcast, and the suggester is not asked again.")]
    public async Task continue_submits_as_podcast()
    {
        // Arrange
        var result = Result();
        ArrangeResults(result);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var submitted = await sut.SubmitAsync(Request(result, "continue"), CancellationToken.None);

        // Assert
        submitted.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        submitted.Outcome!.Results.Should().ContainSingle().Which.DiscoveryItemId.Should().Be(result.Id);
        var options = _persisted.Should().ContainSingle().Subject;
        options.KindChoice.Should().Be(DiscoveryKindChoice.Continue);
        options.ClassificationSignals.Should().BeNull();
        options.TargetFilmId.Should().BeNull();
        options.TargetNewsReportId.Should().BeNull();
        _mocker.GetMock<IDiscoveryKindSuggester>().Verify(
            s => s.SuggestAsync(It.IsAny<DiscoveryResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A cancel decision drops the item: the submit succeeds with empty results and nothing is probed, suggested or persisted.")]
    public async Task cancel_drops_the_item()
    {
        // Arrange
        var result = Result();
        ArrangeResults(result);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var submitted = await sut.SubmitAsync(Request(result, "cancel"), CancellationToken.None);

        // Assert
        submitted.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        submitted.Outcome!.Results.Should().BeEmpty();
        _persisted.Should().BeEmpty();
        _mocker.GetMock<IDiscoveryUrlSubmitter>().Verify(
            s => s.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()),
            Times.Never);
        _mocker.GetMock<IDiscoveryKindSuggester>().Verify(
            s => s.SuggestAsync(It.IsAny<DiscoveryResult>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.TryClaimForProcessing(
            It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A select with a recognised film key passes MadeAsFilm ClassificationSignals and the TargetFilmId, and nothing is suggested.")]
    public async Task select_with_film_key_passes_signals_and_target_film_id()
    {
        // Arrange
        var result = Result();
        var filmId = _fixture.Create<Guid>();
        ArrangeResults(result);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var submitted = await sut.SubmitAsync(
            Request(result, "select", DiscoveryKindKeys.Film(filmId)), CancellationToken.None);

        // Assert
        submitted.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        var options = _persisted.Should().ContainSingle().Subject;
        options.KindChoice.Should().Be(DiscoveryKindChoice.Select);
        options.SelectedKindKey.Should().Be(DiscoveryKindKeys.Film(filmId));
        options.ClassificationSignals.Should().NotBeNull();
        options.ClassificationSignals!.MadeAsFilm.Should().BeTrue();
        options.ClassificationSignals.News.Should().BeFalse();
        options.TargetFilmId.Should().Be(filmId);
        options.TargetNewsReportId.Should().BeNull();
        options.Parent.Should().BeNull();
        _mocker.GetMock<IDiscoveryKindSuggester>().Verify(
            s => s.SuggestAsync(It.IsAny<DiscoveryResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A select with a recognised news-report key passes News ClassificationSignals, the organisation parent, and the TargetNewsReportId.")]
    public async Task select_with_news_report_key_passes_signals_and_target_ids()
    {
        // Arrange
        var result = Result();
        var organisationId = _fixture.Create<Guid>();
        var reportId = _fixture.Create<Guid>();
        ArrangeResults(result);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var submitted = await sut.SubmitAsync(
            Request(result, "select", DiscoveryKindKeys.NewsReport(organisationId, reportId)),
            CancellationToken.None);

        // Assert
        submitted.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        var options = _persisted.Should().ContainSingle().Subject;
        options.KindChoice.Should().Be(DiscoveryKindChoice.Select);
        options.ClassificationSignals.Should().NotBeNull();
        options.ClassificationSignals!.News.Should().BeTrue();
        options.ClassificationSignals.MadeAsFilm.Should().BeFalse();
        options.TargetNewsOrganisationId.Should().Be(organisationId);
        options.TargetNewsReportId.Should().Be(reportId);
        options.TargetFilmId.Should().BeNull();
        options.Parent.Should().NotBeNull();
        options.Parent!.Id.Should().Be(organisationId);
    }

    [Fact(DisplayName =
        "A select with a missing kind key returns InvalidKindDecision, nothing is claimed or submitted, and the item is not created as a podcast.")]
    public async Task select_with_missing_key_is_invalid_and_ingests_nothing()
    {
        // Arrange
        var result = Result();
        ArrangeResults(result);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var submitted = await sut.SubmitAsync(Request(result, "select"), CancellationToken.None);

        // Assert
        AssertInvalidKindDecision(submitted, result.Id);
    }

    [Fact(DisplayName =
        "A select with an unrecognised kind key returns InvalidKindDecision, nothing is claimed or submitted, and the item is not created as a podcast.")]
    public async Task select_with_unrecognised_key_is_invalid_and_ingests_nothing()
    {
        // Arrange
        var result = Result();
        ArrangeResults(result);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var submitted = await sut.SubmitAsync(
            Request(result, "select", _fixture.Create<string>()), CancellationToken.None);

        // Assert
        AssertInvalidKindDecision(submitted, result.Id);
    }

    [Fact(DisplayName =
        "A mapped-channel item is never offered a suggestion: when the suggester returns no hint, the submit is not held and the item is persisted as a podcast.")]
    public async Task mapped_channel_is_not_offered_a_suggestion()
    {
        // Arrange
        var result = Result();
        _suggestion = null;
        ArrangeResults(result);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var submitted = await sut.SubmitAsync(Request(result), CancellationToken.None);

        // Assert
        submitted.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        submitted.Outcome!.Results.Should().ContainSingle().Which.DiscoveryItemId.Should().Be(result.Id);
        var options = _persisted.Should().ContainSingle().Subject;
        options.KindChoice.Should().Be(DiscoveryKindChoice.Unset);
        options.ClassificationSignals.Should().BeNull();
        options.TargetFilmId.Should().BeNull();
        options.TargetNewsReportId.Should().BeNull();
        _mocker.GetMock<IDiscoveryKindSuggester>().Verify(
            s => s.SuggestAsync(It.Is<DiscoveryResult>(r => r.Id == result.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void AssertInvalidKindDecision(DiscoveryCurationSubmitResult submitted, Guid resultId)
    {
        submitted.Status.Should().Be(DiscoveryCurationSubmitStatus.InvalidKindDecision);
        submitted.Outcome!.ErrorsOccurred.Should().BeTrue();
        submitted.Outcome.Results.Should().ContainSingle().Which.DiscoveryItemId.Should().Be(resultId);
        _persisted.Should().BeEmpty();
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.TryClaimForProcessing(
            It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDiscoveryKindSuggester>().Verify(
            s => s.SuggestAsync(It.IsAny<DiscoveryResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private DiscoveryResult Result() => new()
    {
        Id = _fixture.Create<Guid>(),
        EpisodeName = _fixture.Create<string>(),
        Urls = { YouTube = new Uri("https://www.youtube.com/watch?v=abcdefghijk") }
    };

    private void ArrangeResults(DiscoveryResult result)
    {
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync([result]);
    }

    private DiscoverySubmitRequest Request(DiscoveryResult result, string? kindDecision = null, string? selectedKey = null) =>
        new()
        {
            DiscoveryResultsDocumentIds = [_documentId],
            ResultIds = [result.Id],
            KindDecisions = kindDecision == null ? null : new Dictionary<Guid, string> { [result.Id] = kindDecision },
            SelectedKindKeys = selectedKey == null ? null : new Dictionary<Guid, string> { [result.Id] = selectedKey }
        };

    private CatalogueAuthorityCandidate Candidate(string key) =>
        new(key, _fixture.Create<string>(), Year: null, CatalogueAuthorityCandidate.FilmKind,
            DiscoveryKindKeys.CatalogueSource, TmdbId: null, TvdbId: null);

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items.ToList())
        {
            yield return item;
            await Task.Yield();
        }
    }
}
