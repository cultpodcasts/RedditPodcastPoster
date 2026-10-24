using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services;

public class SubmitUrlServiceTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IReadOnlyList<Podcast> _storedPodcasts = [];
    private SubmitOptions? _capturedOptions;

    public SubmitUrlServiceTests()
    {
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> predicate) =>
                ToAsyncEnumerable(_storedPodcasts.Where(predicate.Compile())));

        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync((Uri _, IndexingContext __, SubmitOptions options) =>
            {
                _capturedOptions = options;
                return new SubmitResult(SubmitResultState.None, SubmitResultState.None);
            });

        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisode(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse());
    }

    [Fact(DisplayName =
        "A URL routed to a news organisation is indexed immediately as a NewsReport search document keyed by the playable id, under the organisation's name, without the podcast-episode indexer.")]
    public async Task routed_news_report_is_indexed_immediately()
    {
        // Arrange
        var organisation = new NewsOrganisation("Synthetic News " + _fixture.CreateTitle()) { SearchTerms = "synthetic terms" };
        var transient = new CatalogueIndexingSource().ToTransientPodcast(organisation);
        var episode = _fixture.CreateEpisode();
        IReadOnlyList<EpisodeSearchRecord>? uploaded = null;
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<EpisodeSearchRecord> docs, CancellationToken _) => uploaded = docs)
            .ReturnsAsync(true);
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                ContentKind: SubmitClassification.NewsReport,
                PlayableId: episode.Id,
                CatalogueSearchSource: new CatalogueSearchSource(transient, episode, CatalogueParentKind.NewsOrganisation)));
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(
            new SubmitUrlRequest { Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}") },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        uploaded.Should().ContainSingle();
        uploaded![0].Id.Should().Be(episode.Id.ToString());
        uploaded[0].ContentKind.Should().Be(SearchContentKind.NewsReport);
        uploaded[0].SeriesName.Should().Be(organisation.Name);
        uploaded[0].PublisherSearchTerms.Should().Be("synthetic terms");
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(
            s => s.IndexEpisode(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A failed immediate index of a routed item is logged and the submit still succeeds.")]
    public async Task routed_index_failure_still_ok()
    {
        // Arrange
        var show = new RedditPodcastPoster.Models.TvShows.TvShow("Synthetic Show " + _fixture.CreateTitle());
        var episode = _fixture.CreateEpisode();
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic"));
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                ContentKind: SubmitClassification.TvShowEpisode,
                PlayableId: episode.Id,
                CatalogueSearchSource: new CatalogueSearchSource(
                    new CatalogueIndexingSource().ToTransientPodcast(show), episode, CatalogueParentKind.TvShow)));
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(
            new SubmitUrlRequest { Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}") },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        result.Result!.PlayableId.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When several podcasts share a name and submit is name-only, the API returns conflict instead of attaching to the first Cosmos row.")]
    public async Task name_only_ambiguous_returns_conflict_without_submit()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var first = _fixture.CreatePodcast(p => p.Name = name);
        var second = _fixture.CreatePodcast(p => p.Name = name);
        _storedPodcasts = [first, second];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Conflict);
        result.AmbiguousPodcasts.Should().BeEquivalentTo([first.Id, second.Id]);
        _mocker.GetMock<IUrlSubmitter>().Verify(
            s => s.Submit(It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When exactly one podcast has the submitted name, submit attaches using that podcast's id.")]
    public async Task unique_name_resolves_to_podcast_id()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast(p => p.Name = name);
        _storedPodcasts = [podcast];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions.Should().NotBeNull();
        _capturedOptions!.PodcastId.Should().Be(podcast.Id);
        _capturedOptions.PodcastName.Should().Be(name);
    }

    [Fact(DisplayName =
        "When Cosmos has one series whose name differs only by case, name-only submit attaches that id.")]
    public async Task unique_name_case_insensitive_resolves_to_podcast_id()
    {
        // Arrange
        var storedName = _fixture.CreateTitle();
        var submittedName = FlipCasing(storedName);
        var podcast = _fixture.CreatePodcast(p => p.Name = storedName);
        _storedPodcasts = [podcast];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = submittedName
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions!.PodcastId.Should().Be(podcast.Id);
    }

    [Fact(DisplayName =
        "When submit already includes podcastId, name lookup is skipped so a curator choice is not overwritten.")]
    public async Task podcast_id_is_not_replaced_by_name_lookup()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var chosen = _fixture.CreateGuid();
        var other = _fixture.CreatePodcast(p => p.Name = name);
        _storedPodcasts = [other];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastId = chosen,
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions!.PodcastId.Should().Be(chosen);
        _mocker.GetMock<IPodcastRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the submitted name matches no podcast, submit continues without an id so a series can be created.")]
    public async Task missing_name_leaves_podcast_id_unset()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        _storedPodcasts = [];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions!.PodcastId.Should().BeNull();
        _capturedOptions.PodcastName.Should().Be(name);
    }

    [Fact(DisplayName =
        "When submit references a podcast id that does not exist, the API returns not-found instead of creating a series.")]
    public async Task missing_podcast_id_maps_to_not_found()
    {
        // Arrange
        var missingId = _fixture.CreateGuid();
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new SubmitPodcastNotFoundException(missingId));
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastId = missingId
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.PodcastNotFound);
    }

    [Fact(DisplayName =
        "When the submitter reports an ambiguous name, the API maps that to conflict with the podcast id list.")]
    public async Task submitter_ambiguous_name_maps_to_conflict()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var firstId = _fixture.CreateGuid();
        var secondId = _fixture.CreateGuid();
        _storedPodcasts = [];
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new AmbiguousPodcastNameException(name, [firstId, secondId]));
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Conflict);
        result.AmbiguousPodcasts.Should().BeEquivalentTo([firstId, secondId]);
    }

    [Fact(DisplayName =
        "When two TvShows share a name, submit is a 409 that keeps the parent ids and includes content kind TvShowEpisode and the parent name.")]
    public async Task ambiguous_tv_show_name_is_a_conflict_with_kind_and_parent_name()
    {
        // Arrange
        var parentName = _fixture.CreateTitle();
        var firstId = _fixture.CreateGuid();
        var secondId = _fixture.CreateGuid();
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new AmbiguousParentNameException(
                SubmitClassification.TvShowEpisode,
                parentName,
                [firstId, secondId]));
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}")
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Conflict);
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.ParentName.Should().Be(parentName);
        result.AmbiguousPodcasts.Should().BeEquivalentTo([firstId, secondId]);
    }

    [Fact(DisplayName =
        "When submit classifies a URL as rejected, the API does not return success.")]
    public async Task rejected_classification_is_not_ok()
    {
        // Arrange
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: SubmitClassification.Episode,
                Rejected: true));
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}")
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Rejected);
        result.Result!.ContentKind.Should().Be(SubmitClassification.Episode);
        result.Result.Rejected.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When submit needs a curator, the API does not return success.")]
    public async Task curator_hold_is_not_ok()
    {
        // Arrange
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: SubmitClassification.Episode,
                RequiresCurator: true));
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}")
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.RequiresCurator);
        result.Result!.RequiresCurator.Should().BeTrue();
        result.Result.ContentKind.Should().Be(SubmitClassification.Episode);
    }

    [Fact(DisplayName =
        "When a TV submit has no series name, the API does not return success " +
        "and reports that the series name is required.")]
    public async Task missing_series_name_is_not_ok()
    {
        // Arrange
        var message = "TV submit needs a series name before it can be stored.";
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new InvalidOperationException(message));
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}")
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().NotBe(SubmitUrlStatus.Ok);
        result.Status.Should().Be(SubmitUrlStatus.Failed);
        result.Message.Should().Be(message);
    }

    [Fact(DisplayName =
        "When submit includes PrefetchedMeta from prepare cache, SubmitOptions forwards that specimen " +
        "so the submitter can skip a second scrape (submitUsesPrefetchedMetaWhenCached).")]
    public async Task prefetched_meta_is_forwarded_into_submit_options()
    {
        // Arrange
        var meta = new NonPodcastServiceItemMetaData(
            Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>(),
            ShowName: _fixture.CreateTitle(),
            Publisher: _fixture.Create<string>());
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PrefetchedMeta = meta
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions.Should().NotBeNull();
        _capturedOptions!.PrefetchedMeta.Should().Be(meta);
    }

    [Fact(DisplayName =
        "When PrefetchedMeta is omitted on submit, SubmitOptions.PrefetchedMeta stays null " +
        "so the submitter falls back to live extract.")]
    public async Task omitted_prefetched_meta_leaves_submit_options_null()
    {
        // Arrange
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}")
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions.Should().NotBeNull();
        _capturedOptions!.PrefetchedMeta.Should().BeNull();
    }

    [Fact(DisplayName =
        "Submit URL DTO ToModel round-trips identityDecision select and selectedIdentityKey into SubmitOptions " +
        "so a curator can complete an ambiguous identity hold.")]
    public async Task identity_decision_select_round_trips_into_submit_options()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var selectedKey = $"tmdb:{tmdbId}";
        var dto = new global::Api.Dtos.SubmitUrlRequest
        {
            Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
            IdentityDecision = "select",
            SelectedIdentityKey = selectedKey
        };
        var request = dto.ToModel();
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions.Should().NotBeNull();
        _capturedOptions!.IdentityChoice.Should().Be(
            RedditPodcastPoster.Catalogue.Authority.CatalogueIdentityChoice.Select);
        _capturedOptions.SelectedIdentityKey.Should().Be(selectedKey);
    }

    [Fact(DisplayName =
        "When adding a supported language fails with CatalogueLanguageRegistrationException, " +
        "submit returns BadRequest so the handler responds 400 instead of InternalError.")]
    public async Task language_registration_failure_is_bad_request()
    {
        // Arrange
        var message = "Could not add the supported language.";
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new RedditPodcastPoster.UrlSubmission.Languages.CatalogueLanguageRegistrationException(message));
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(
            new SubmitUrlRequest { Url = new Uri($"https://example.com/{_fixture.Create<string>()}") },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.BadRequest);
        result.Message.Should().Be(message);
    }

    private static string FlipCasing(string value)
    {
        var upper = value.ToUpperInvariant();
        return string.Equals(upper, value, StringComparison.Ordinal)
            ? value.ToLowerInvariant()
            : upper;
    }

    private static async IAsyncEnumerable<Podcast> ToAsyncEnumerable(IEnumerable<Podcast> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
