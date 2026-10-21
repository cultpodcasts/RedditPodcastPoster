using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.Discovery;

/// <summary>
/// Discovery results stored under a news organisation / TV show (curator submit and supervisor apply) are reported and
/// indexed as such, and curation shows such parents as matched owners. All ids and names are synthetic.
/// </summary>
public class DiscoveryCatalogueParentRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IReadOnlyList<DiscoveryResult> _results = [];
    private DiscoverySubmitResult _submitResult = new(DiscoverySubmitResultState.NoUrls);
    private readonly List<Guid> _indexedEpisodeIds = [];
    private readonly List<EpisodeSearchRecord> _uploaded = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<NewsReport> _reports = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];

    public DiscoveryCatalogueParentRules()
    {
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.TryClaimForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync(() => _results);
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(() => _submitResult);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<Guid> ids, CancellationToken _) => _indexedEpisodeIds.AddRange(ids))
            .ReturnsAsync(new EntitySearchIndexerResponse());
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<EpisodeSearchRecord> docs, CancellationToken _) => _uploaded.AddRange(docs))
            .ReturnsAsync(true);

        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(ToAsync(Array.Empty<Podcast>()));
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns(ToAsync(Array.Empty<Episode>()));
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> p) => ToAsync(_organisations.Where(p.Compile())));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> p) => ToAsync(_shows.Where(p.Compile())));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns((Guid id) => ToAsync(_reports.Where(x => x.NewsOrganisationId == id)));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns((Guid id) => ToAsync(_tvEpisodes.Where(x => x.TvShowId == id)));
    }

    private DiscoverySubmitRequest ArrangeOneResult()
    {
        var result = new DiscoveryResult();
        _results = [result];
        return new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = [_fixture.CreateGuid()], ResultIds = [result.Id] };
    }

    [Fact(DisplayName =
        "A discovery result stored as a news report reports the report and organisation ids with its content kind, and is indexed straight away as a news report document, not through the podcast-episode indexer (which cannot find it).")]
    public async Task news_report_outcome_and_indexing()
    {
        // Arrange
        var request = ArrangeOneResult();
        var organisation = new NewsOrganisation(_fixture.CreateTitle());
        var transient = new CatalogueIndexingSource().ToTransientPodcast(organisation);
        var episode = _fixture.CreateEpisode();
        _submitResult = new DiscoverySubmitResult(DiscoverySubmitResultState.CreatedEpisode,
            ContentKind: SubmitClassification.NewsReport, PlayableId: episode.Id, ParentId: organisation.Id,
            CatalogueSearchSource: new CatalogueSearchSource(transient, episode, CatalogueParentKind.NewsOrganisation));
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        var outcome = result.Outcome!.Results.Should().ContainSingle().Subject;
        outcome.EpisodeId.Should().Be(episode.Id);
        outcome.PodcastId.Should().Be(organisation.Id);
        outcome.ContentKind.Should().Be(SubmitClassification.NewsReport);
        outcome.Message.Should().Be(nameof(DiscoverySubmitResultState.CreatedEpisode));
        _uploaded.Should().ContainSingle();
        _indexedEpisodeIds.Should().NotContain(episode.Id);
    }

    [Fact(DisplayName =
        "A discovery result stored as a podcast episode is still indexed through the podcast-episode indexer and carries no content kind.")]
    public async Task podcast_episode_outcome_and_indexing_unchanged()
    {
        // Arrange
        var request = ArrangeOneResult();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        _submitResult = new DiscoverySubmitResult(DiscoverySubmitResultState.CreatedEpisode, episode);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        var outcome = result.Outcome!.Results.Should().ContainSingle().Subject;
        outcome.EpisodeId.Should().Be(episode.Id);
        outcome.PodcastId.Should().Be(podcast.Id);
        outcome.ContentKind.Should().BeNull();
        _indexedEpisodeIds.Should().Equal(episode.Id);
        _uploaded.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A discovery result whose matched id is a news organisation (a migrated podcast keeps its id) shows the organisation's name, visibility and not-removed report count with kind NewsOrganisation, instead of an unknown channel.")]
    public async Task matched_news_organisation_is_described()
    {
        // Arrange
        var organisation = new NewsOrganisation(_fixture.CreateTitle());
        _organisations.Add(organisation);
        _reports.Add(new NewsReport(_fixture.CreateTitle()) { NewsOrganisationId = organisation.Id });
        _reports.Add(new NewsReport(_fixture.CreateTitle()) { NewsOrganisationId = organisation.Id, Removed = true });
        var result = new DiscoveryResult { MatchingPodcastIds = [organisation.Id] };
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        var info = await sut.GetPodcastInfo([result], CancellationToken.None);

        // Assert
        info.Should().ContainKey(organisation.Id);
        info[organisation.Id].Should().Be(new DiscoveryPodcastInfo(organisation.Name, true, 1,
            nameof(CatalogueParentKind.NewsOrganisation)));
    }

    [Fact(DisplayName =
        "A matched id that is a removed TV show is described as a not-visible TvShow, so the curator sees the show was removed.")]
    public async Task matched_removed_tv_show_is_described()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Removed = true };
        _shows.Add(show);
        _tvEpisodes.Add(new TvShowEpisode(_fixture.CreateTitle()) { TvShowId = show.Id });
        var result = new DiscoveryResult { MatchingPodcastIds = [show.Id] };
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        var info = await sut.GetPodcastInfo([result], CancellationToken.None);

        // Assert
        info[show.Id].Should().Be(new DiscoveryPodcastInfo(show.Name, false, 1, nameof(CatalogueParentKind.TvShow)));
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items.ToList())
        {
            yield return item;
            await Task.Yield();
        }
    }
}
