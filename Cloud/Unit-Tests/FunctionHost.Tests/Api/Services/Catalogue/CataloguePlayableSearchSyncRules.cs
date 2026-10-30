using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Services.Catalogue;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;
using SocialPostRemovalTarget = Api.Services.Catalogue.SocialPostRemovalTarget;

namespace FunctionHost.Tests.Api.Services.Catalogue;

public class CataloguePlayableSearchSyncRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<EpisodeSearchRecord> _uploaded = [];
    private readonly List<PodcastEpisode> _shortLinkDeletes = [];
    private bool _uploadSucceeds = true;

    public CataloguePlayableSearchSyncRules()
    {
        _mocker.Use(NullLogger<CataloguePlayableSearchSync>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploaded.AddRange(documents))
            .ReturnsAsync(() => _uploadSucceeds);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(target => _shortLinkDeletes.Add(target))
            .ReturnsAsync(new DeleteResult(true));
    }

    private CataloguePlayableSearchSync Sut => _mocker.CreateInstance<CataloguePlayableSearchSync>();

    private NewsReport CreateReport(Action<NewsReport>? customise = null)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            SearchTerms = _fixture.CreateTitle()
        };
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Length = _fixture.CreateDuration(),
            SearchTerms = _fixture.CreateTitle()
        };
        report.SetNewsOrganisationProperties(organisation);
        customise?.Invoke(report);
        return report;
    }

    private static PodcastEpisode ShortLinkTarget(NewsReport report) =>
        SocialPostRemovalTarget.For(report).Projection;

    [Fact(DisplayName =
        "Plain English rule: when a news report edit is not a removal, then its search document is pushed straight away as a NewsReport under the organisation, because a curator edit must show in search without waiting for the pull indexer.")]
    public async Task edit_pushes_news_report_document()
    {
        // Arrange
        var report = CreateReport();

        // Act
        var response = await Sut.AfterSave(report, ShortLinkTarget(report), removedByChange: false, CancellationToken.None);

        // Assert
        response!.IndexerState.Should().Be(IndexerState.Executed);
        var document = _uploaded.Should().ContainSingle().Subject;
        document.Id.Should().Be(report.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.NewsReport);
        document.Title.Should().Be(report.Title);
        document.SeriesName.Should().Be(report.NewsOrganisationName);
        document.PublisherSearchTerms.Should().Be(report.PublisherSearchTerms);
        document.EpisodeSearchTerms.Should().Be(report.SearchTerms);
        _search.DeletedKeys.Should().BeEmpty();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when an edit removes the item, then the search document and the short-link record are deleted, as a podcast episode removal does.")]
    public async Task removal_deletes_search_document_and_short_link()
    {
        // Arrange
        var report = CreateReport(r => r.Removed = true);

        // Act
        var response = await Sut.AfterSave(report, ShortLinkTarget(report), removedByChange: true, CancellationToken.None);

        // Assert
        response.Should().BeNull();
        _search.DeletedKeys.Should().Equal(report.Id.ToString());
        _shortLinkDeletes.Should().ContainSingle().Which.Episode.Id.Should().Be(report.Id);
        _uploaded.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when the item's parent is removed, then an edit deletes the search document instead of pushing it and keeps the short link, because the pull indexer's filter leaves items of removed parents out of search.")]
    public async Task edit_under_removed_parent_deletes_search_document_only()
    {
        // Arrange
        var report = CreateReport(r => r.ParentRemoved = true);

        // Act
        await Sut.AfterSave(report, ShortLinkTarget(report), removedByChange: false, CancellationToken.None);

        // Assert
        _search.DeletedKeys.Should().Equal(report.Id.ToString());
        _uploaded.Should().BeEmpty();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when the search push fails, then the step reports a failed indexer state and does not throw, because the Cosmos save already succeeded and the pull indexer will catch up.")]
    public async Task failed_push_reports_failure_without_throwing()
    {
        // Arrange
        var report = CreateReport();
        _uploadSucceeds = false;

        // Act
        var response = await Sut.AfterSave(report, ShortLinkTarget(report), removedByChange: false, CancellationToken.None);

        // Assert
        response!.IndexerState.Should().Be(IndexerState.Failure);
    }

    [Fact(DisplayName =
        "Plain English rule: when a TV episode edit is pushed, then the document is a TvShowEpisode under the show name, because TV search results group by show.")]
    public async Task edit_pushes_tv_episode_document()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        episode.SetTvShowProperties(show);

        // Act
        await Sut.AfterSave(episode, SocialPostRemovalTarget.For(episode).Projection,
            removedByChange: false, CancellationToken.None);

        // Assert
        var document = _uploaded.Should().ContainSingle().Subject;
        document.ContentKind.Should().Be(SearchContentKind.TvShowEpisode);
        document.SeriesName.Should().Be(show.Name);
        document.Title.Should().Be(episode.Title);
    }

    [Fact(DisplayName =
        "Plain English rule: when a film edit is pushed, then the document is a Film titled with the film's name and no series, because a film has no parent.")]
    public async Task edit_pushes_film_document()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };

        // Act
        await Sut.AfterSave(film, SocialPostRemovalTarget.For(film).Projection,
            removedByChange: false, CancellationToken.None);

        // Assert
        var document = _uploaded.Should().ContainSingle().Subject;
        document.ContentKind.Should().Be(SearchContentKind.Film);
        document.Title.Should().Be(film.Name);
        document.SeriesName.Should().BeNull();
        document.PublisherSearchTerms.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: after an on-demand index, the saved TV episodes and news reports are pushed to search in one batch and no short link is touched, because the hourly indexer does not touch short links either.")]
    public async Task after_index_pushes_changed_items_in_one_batch()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        episode.SetTvShowProperties(show);
        var report = CreateReport();

        // Act
        var response = await Sut.AfterIndex(show.Name, [episode, report], CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Executed);
        _uploaded.Select(d => d.Id).Should().BeEquivalentTo([episode.Id.ToString(), report.Id.ToString()]);
        _mocker.GetMock<IPlayableSearchDocumentSwap>().Verify(
            s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()), Times.Once);
        _search.DeleteBatches.Should().BeEmpty();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: after an on-demand index, an item the elimination terms removed is deleted from search and not pushed, because removed items must leave search straight away.")]
    public async Task after_index_deletes_removed_items()
    {
        // Arrange
        var removed = CreateReport(r => r.Removed = true);
        var kept = CreateReport();

        // Act
        var response = await Sut.AfterIndex(_fixture.CreateTitle(), [removed, kept], CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Executed);
        _search.DeletedKeys.Should().Equal(removed.Id.ToString());
        _uploaded.Should().ContainSingle().Which.Id.Should().Be(kept.Id.ToString());
    }

    [Fact(DisplayName =
        "Plain English rule: after an on-demand index that saved nothing, search is not called and the state is NoDocuments, because there is nothing to push.")]
    public async Task after_index_with_no_changes_does_nothing()
    {
        // Arrange
        var parentName = _fixture.CreateTitle();

        // Act
        var response = await Sut.AfterIndex(parentName, [], CancellationToken.None);

        // Assert
        response.EpisodeIndexRequestState.Should().Be(EpisodeIndexRequestState.NoDocuments);
        _uploaded.Should().BeEmpty();
        _search.DeleteBatches.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: after an on-demand index, a failed push is reported as Failure and not thrown, because the items are already saved and the pull indexer will pick them up.")]
    public async Task after_index_reports_push_failure()
    {
        // Arrange
        _uploadSucceeds = false;
        var report = CreateReport();

        // Act
        var response = await Sut.AfterIndex(_fixture.CreateTitle(), [report], CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Failure);
    }
}
