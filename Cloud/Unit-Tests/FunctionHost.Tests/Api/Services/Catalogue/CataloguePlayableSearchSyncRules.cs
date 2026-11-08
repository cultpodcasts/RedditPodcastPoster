using FluentAssertions;
using Microsoft.Extensions.Logging;
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
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
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
    private readonly Dictionary<Guid, NewsOrganisation> _organisations = new();
    private readonly Dictionary<Guid, TvShow> _shows = new();
    private bool _uploadSucceeds = true;

    public CataloguePlayableSearchSyncRules()
    {
        _mocker.GetMock<ILogger<CataloguePlayableSearchSync>>();
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploaded.AddRange(documents))
            .ReturnsAsync(() => _uploadSucceeds);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(target => _shortLinkDeletes.Add(target))
            .ReturnsAsync(new DeleteResult(true));
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.GetValueOrDefault(id));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.GetValueOrDefault(id));
    }

    private CataloguePlayableSearchSync Sut => _mocker.CreateInstance<CataloguePlayableSearchSync>();

    private NewsReport CreateReport(Action<NewsReport>? customise = null, bool organisationRemoved = false)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            SearchTerms = _fixture.CreateTitle(),
            Removed = organisationRemoved
        };
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Length = _fixture.CreateDuration(),
            SearchTerms = _fixture.CreateTitle()
        };
        report.SetNewsOrganisationProperties(organisation);
        customise?.Invoke(report);
        _organisations[organisation.Id] = organisation;
        return report;
    }

    private TvShowEpisode CreateEpisode(Action<TvShowEpisode>? customise = null, bool showRemoved = false)
    {
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Removed = showRemoved
        };
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Length = _fixture.CreateDuration()
        };
        episode.SetTvShowProperties(show);
        customise?.Invoke(episode);
        _shows[show.Id] = show;
        return episode;
    }

    private static PodcastEpisode ShortLinkTarget(NewsReport report) =>
        SocialPostRemovalTarget.For(report).Projection;

    private static PodcastEpisode ShortLinkTarget(TvShowEpisode episode) =>
        SocialPostRemovalTarget.For(episode).Projection;

    private void VerifyParentMissLogged(params string[] fragments) =>
        _mocker.GetMock<ILogger<CataloguePlayableSearchSync>>().Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => fragments.All(fragment => state!.ToString()!.Contains(fragment))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

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
        "Plain English rule: when AfterHardDelete runs after a Cosmos hard delete, then the search document and the short-link record are deleted, " +
        "because hard delete must leave the same cleanup as soft-remove and must not rely on the pull indexer alone.")]
    public async Task after_hard_delete_deletes_search_document_and_short_link()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        var shortLinkTarget = SocialPostRemovalTarget.For(film).Projection;

        // Act
        await Sut.AfterHardDelete(film.Id, film.Name, shortLinkTarget, CancellationToken.None);

        // Assert
        _search.DeletedKeys.Should().Equal(film.Id.ToString());
        _shortLinkDeletes.Should().ContainSingle().Which.Episode.Id.Should().Be(film.Id);
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
        "Plain English rule: when the live news organisation is Removed but ParentRemoved still lags false, " +
        "then an edit deletes the search document instead of pushing it, because ParentChangeCascade can leave denorm stale.")]
    public async Task edit_under_live_removed_parent_with_lagging_denorm_deletes_search_document()
    {
        // Arrange
        var report = CreateReport(organisationRemoved: true);
        report.ParentRemoved = false;

        // Act
        await Sut.AfterSave(report, ShortLinkTarget(report), removedByChange: false, CancellationToken.None);

        // Assert
        _search.DeletedKeys.Should().Equal(report.Id.ToString());
        _uploaded.Should().BeEmpty();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when the live TV show is Removed but ParentRemoved still lags false, " +
        "then an edit deletes the search document instead of pushing it, because ParentChangeCascade can leave denorm stale.")]
    public async Task edit_under_live_removed_tv_show_with_lagging_denorm_deletes_search_document()
    {
        // Arrange
        var episode = CreateEpisode(showRemoved: true);
        episode.ParentRemoved = false;

        // Act
        await Sut.AfterSave(episode, ShortLinkTarget(episode), removedByChange: false, CancellationToken.None);

        // Assert
        _search.DeletedKeys.Should().Equal(episode.Id.ToString());
        _uploaded.Should().BeEmpty();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when AfterSave cannot load the live news organisation, then eligibility falls open to " +
        "denormalised ParentRemoved and a Warning with prefix CataloguePlayableSearchSyncParentMiss is logged, " +
        "because orphans must still push while a ParentChangeCascade-lag Cosmos miss stays observable.")]
    public async Task edit_with_null_parent_lookup_fails_open_and_logs_parent_miss()
    {
        // Arrange — CreateReport seeds _organisations; remove so GetNewsOrganisation returns null.
        var report = CreateReport();
        report.ParentRemoved = false;
        _organisations.Clear();

        // Act
        var response = await Sut.AfterSave(report, ShortLinkTarget(report), removedByChange: false, CancellationToken.None);

        // Assert
        response!.IndexerState.Should().Be(IndexerState.Executed);
        _uploaded.Should().ContainSingle().Which.Id.Should().Be(report.Id.ToString());
        _search.DeletedKeys.Should().BeEmpty();
        VerifyParentMissLogged(
            "CataloguePlayableSearchSyncParentMiss",
            report.NewsOrganisationId.ToString(),
            report.Id.ToString());
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
        var episode = CreateEpisode();

        // Act
        await Sut.AfterSave(episode, ShortLinkTarget(episode), removedByChange: false, CancellationToken.None);

        // Assert
        var document = _uploaded.Should().ContainSingle().Subject;
        document.ContentKind.Should().Be(SearchContentKind.TvShowEpisode);
        document.SeriesName.Should().Be(episode.TvShowName);
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
        "Plain English rule: after an on-demand index, the saved TV episodes are pushed to search in one batch and no short link is touched, because the hourly indexer does not touch short links either.")]
    public async Task after_index_pushes_changed_items_in_one_batch()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        episode.SetTvShowProperties(show);
        var second = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        second.SetTvShowProperties(show);

        // Act
        var response = await Sut.AfterIndex(show, [episode, second], CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Executed);
        _uploaded.Select(d => d.Id).Should().BeEquivalentTo([episode.Id.ToString(), second.Id.ToString()]);
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
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var removed = CreateReport(r => r.Removed = true);
        var kept = CreateReport();
        // Homogeneous Index batch under one live parent (as CatalogueParentIndexService does).
        removed.NewsOrganisationId = organisation.Id;
        kept.NewsOrganisationId = organisation.Id;

        // Act
        var response = await Sut.AfterIndex(organisation, [removed, kept], CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Executed);
        _search.DeletedKeys.Should().Equal(removed.Id.ToString());
        _uploaded.Should().ContainSingle().Which.Id.Should().Be(kept.Id.ToString());
    }

    [Fact(DisplayName =
        "Plain English rule: after an on-demand index of a Removed parent whose children's ParentRemoved still lags false, " +
        "every child is deleted from search and not pushed, because live parent Removed is authoritative.")]
    public async Task after_index_under_removed_parent_deletes_lagging_children()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = true };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        episode.SetTvShowProperties(show);
        episode.ParentRemoved = false;

        // Act
        var response = await Sut.AfterIndex(show, [episode], CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Executed);
        _search.DeletedKeys.Should().Equal(episode.Id.ToString());
        _uploaded.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: after an on-demand index that saved nothing, search is not called and the state is NoDocuments, because there is nothing to push.")]
    public async Task after_index_with_no_changes_does_nothing()
    {
        // Arrange
        var parent = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };

        // Act
        var response = await Sut.AfterIndex(parent, [], CancellationToken.None);

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
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var report = CreateReport();
        report.NewsOrganisationId = organisation.Id;

        // Act
        var response = await Sut.AfterIndex(organisation, [report], CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Failure);
    }
}
