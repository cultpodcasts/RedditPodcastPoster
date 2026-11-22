using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.UrlShortening.Services;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Episodes;

public class YouTubeAlternateAttachServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private readonly List<Guid> _searchDeletes = [];
    private bool _searchUploadSucceeds = true;
    private bool _searchDeleteSucceeds = true;
    private NewsReport? _news;
    private string _newsEtag = "1";
    private int _newsReplaces;
    private bool _failNextNewsReplace;
    private TvShowEpisode? _tv;
    private string _tvEtag = "1";
    private int _tvReplaces;
    private bool _failNextTvReplace;

    public YouTubeAlternateAttachServiceRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use(NullLogger<YouTubeAlternateAttachService>.Instance);
        _mocker.GetMock<IEpisodeServiceMaterialiser>()
            .Setup(m => m.MaterialiseAsync(It.IsAny<Podcast>(), It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((docs, _) => _uploads.Add(docs.ToList()))
            .ReturnsAsync(() => _searchUploadSucceeds);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.DeleteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => _searchDeletes.AddRange(ids))
            .ReturnsAsync(() => _searchDeleteSucceeds);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Redirect(It.IsAny<PodcastEpisode>(), It.IsAny<string>()))
            .ReturnsAsync(new WriteResult(true));

        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((Expression<Func<NewsReport, bool>> predicate) =>
                _news is not null && predicate.Compile()(_news) ? _news : null);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetNewsReportWithETag(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid organisationId, Guid id) =>
                _news is { } report && report.Id == id && report.NewsOrganisationId == organisationId
                    ? new ETagged<NewsReport>(CloneNews(report), _newsEtag)
                    : null);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.TryReplace(It.IsAny<NewsReport>(), It.IsAny<string>()))
            .ReturnsAsync((NewsReport report, string etag) =>
            {
                _newsReplaces++;
                if (_failNextNewsReplace)
                {
                    _failNextNewsReplace = false;
                    return false;
                }

                _news = CloneNews(report);
                _newsEtag = (int.Parse(_newsEtag) + 1).ToString();
                return true;
            });

        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> predicate) =>
                _tv is not null && predicate.Compile()(_tv) ? _tv : null);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetTvShowEpisodeWithETag(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid showId, Guid id) =>
                _tv is { } episode && episode.Id == id && episode.TvShowId == showId
                    ? new ETagged<TvShowEpisode>(CloneTv(episode), _tvEtag)
                    : null);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.TryReplace(It.IsAny<TvShowEpisode>(), It.IsAny<string>()))
            .ReturnsAsync((TvShowEpisode episode, string etag) =>
            {
                _tvReplaces++;
                if (_failNextTvReplace)
                {
                    _failNextTvReplace = false;
                    return false;
                }

                _tv = CloneTv(episode);
                _tvEtag = (int.Parse(_tvEtag) + 1).ToString();
                return true;
            });
    }

    private static NewsReport CloneNews(NewsReport report) =>
        System.Text.Json.JsonSerializer.Deserialize<NewsReport>(System.Text.Json.JsonSerializer.Serialize(report))!;

    private static TvShowEpisode CloneTv(TvShowEpisode episode) =>
        System.Text.Json.JsonSerializer.Deserialize<TvShowEpisode>(System.Text.Json.JsonSerializer.Serialize(episode))!;

    private (Podcast Podcast, Episode Episode) SeedSource()
    {
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        return (podcast, episode);
    }

    private static Dictionary<string, ServiceLink> OtherYouTubeServices() =>
        new()
        {
            [ServiceKeys.YouTube] = new() { Url = new Uri("https://www.youtube.com/watch?v=VCRf_qazO9s") }
        };

    private NewsReport SeedNews(Action<NewsReport>? configure = null)
    {
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            NewsOrganisationId = _fixture.CreateGuid(),
            NewsOrganisationName = _fixture.CreateTitle()
        };
        report.YouTubeId = "VCRf_qazO9s";
        report.Services = OtherYouTubeServices();
        configure?.Invoke(report);
        _news = report;
        return report;
    }

    private TvShowEpisode SeedTv(Action<TvShowEpisode>? configure = null)
    {
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            TvShowId = _fixture.CreateGuid(),
            TvShowName = _fixture.CreateTitle()
        };
        episode.YouTubeId = "VCRf_qazO9s";
        episode.Services = OtherYouTubeServices();
        configure?.Invoke(episode);
        _tv = episode;
        return episode;
    }

    private Task<EpisodeYouTubeAlternateAttachResult> Attach(Guid podcastId, Guid episodeId, ContentKind kind, Guid targetId) =>
        _mocker.CreateInstance<YouTubeAlternateAttachService>().AttachAsync(
            new EpisodeYouTubeAlternateAttachCommand(
                podcastId, episodeId, new EpisodeYouTubeAlternateAttachRequest(kind, targetId)),
            CancellationToken.None);

    [Fact(DisplayName =
        "Attaching a YouTube episode to a news report, TV episode or another podcast episode adds the upload as an additional YouTube link, pushes search, and deletes the source row (RPP #1411).")]
    public async Task attach_to_news_tv_and_episode_adds_the_alternate_and_retires_the_source()
    {
        // Arrange
        var (podcast, episode) = SeedSource();
        var news = SeedNews();
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;

        // Act
        var newsResult = await Attach(podcast.Id, episode.Id, ContentKind.NewsReport, news.Id);

        // Assert
        newsResult.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.Accepted);
        _news!.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(youTubeId);
        try
        {
            _ = _episodes.GetStored(episode.Id);
            throw new Exception("source episode should have been deleted");
        }
        catch (KeyNotFoundException)
        {
        }

        var (podcast2, episode2) = SeedSource();
        var tv = SeedTv();
        var tvResult = await Attach(podcast2.Id, episode2.Id, ContentKind.TvShowEpisode, tv.Id);
        tvResult.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.Accepted);
        _tv!.AdditionalYouTube.Should().ContainSingle();
        _tvReplaces.Should().BeGreaterThan(0);

        var (sourcePodcast, sourceEpisode) = SeedSource();
        var targetPodcast = _fixture.CreatePodcast();
        var target = _fixture.CreateStoredEpisodeWithYouTubeOnly(targetPodcast);
        _podcasts.Seed(targetPodcast);
        _episodes.Seed(target);
        var episodeResult = await Attach(sourcePodcast.Id, sourceEpisode.Id, ContentKind.Episode, target.Id);
        episodeResult.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.Accepted);
        _episodes.GetStored(target.Id).AdditionalYouTube.Should().ContainSingle();
        _episodes.Replaces.Should().NotBeEmpty();
    }

    [Fact(DisplayName =
        "Attaching is a conflict naming the holder when the episode's YouTube id already belongs to another item (RPP #1411).")]
    public async Task attach_is_conflict_when_another_item_holds_the_youtube_id()
    {
        // Arrange
        var (podcast, episode) = SeedSource();
        var news = SeedNews();
        var holderId = _fixture.CreateGuid();
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(lookup => lookup.FindCatalogueServiceUrlOwner(
                It.Is<Uri>(url => url.ToString().Contains(youTubeId, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueServiceUrlOwner("Film", null, holderId));

        // Act
        var result = await Attach(podcast.Id, episode.Id, ContentKind.NewsReport, news.Id);

        // Assert
        result.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.Conflict);
        result.Error.Should().Contain(youTubeId);
        result.Error.Should().Contain("Film");
        result.Error.Should().Contain(holderId.ToString());
        _newsReplaces.Should().Be(0);
        _episodes.GetStored(episode.Id).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Attaching to a TV episode retries the ETag replace when the first attempt conflicts, and accepts once the replace succeeds (RPP #1411).")]
    public async Task attach_to_tv_retries_etag_conflict()
    {
        // Arrange
        var (podcast, episode) = SeedSource();
        var tv = SeedTv();
        _failNextTvReplace = true;

        // Act
        var result = await Attach(podcast.Id, episode.Id, ContentKind.TvShowEpisode, tv.Id);

        // Assert
        result.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.Accepted);
        _tvReplaces.Should().Be(2);
        _tv!.AdditionalYouTube.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Attaching a held-for-approval source, a removed target, or the episode to itself is a bad request and writes nothing (RPP #1411).")]
    public async Task attach_refuses_held_source_removed_target_and_self_target()
    {
        // Arrange
        var (podcast, held) = SeedSource();
        held.ApprovalStatus = ApprovalStatus.AwaitingApproval;
        _episodes.Seed(held);
        var news = SeedNews();

        // Act
        var heldResult = await Attach(podcast.Id, held.Id, ContentKind.NewsReport, news.Id);

        var (podcast2, episode2) = SeedSource();
        var removedNews = SeedNews(r => r.Removed = true);
        var removedResult = await Attach(podcast2.Id, episode2.Id, ContentKind.NewsReport, removedNews.Id);

        var selfResult = await Attach(podcast2.Id, episode2.Id, ContentKind.Episode, episode2.Id);

        // Assert
        heldResult.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.BadRequest);
        heldResult.Error.Should().Contain("held for approval");
        removedResult.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.BadRequest);
        removedResult.Error.Should().Contain("removed");
        selfResult.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.BadRequest);
        selfResult.Error.Should().Contain("itself");
        _newsReplaces.Should().Be(0);
    }

    [Fact(DisplayName =
        "When search upload fails after the target is saved, the source episode row is kept so a re-send can resume (RPP #1411).")]
    public async Task attach_keeps_the_source_row_when_search_upload_fails()
    {
        // Arrange
        var (podcast, episode) = SeedSource();
        var news = SeedNews();
        _searchUploadSucceeds = false;

        // Act
        var result = await Attach(podcast.Id, episode.Id, ContentKind.NewsReport, news.Id);

        // Assert
        result.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        _news!.AdditionalYouTube.Should().ContainSingle();
        _episodes.GetStored(episode.Id).Should().NotBeNull();
        _searchDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the target no longer has the merged links on the post-save re-read, the source row is kept and the episode's search document is restored (RPP #1411).")]
    public async Task attach_refuses_to_delete_the_source_when_the_re_read_lost_the_links()
    {
        // Arrange
        var (podcast, episode) = SeedSource();
        var news = SeedNews();
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((Expression<Func<NewsReport, bool>> predicate) =>
            {
                if (_news is null || !predicate.Compile()(_news))
                {
                    return null;
                }

                if (_newsReplaces > 0)
                {
                    return new NewsReport(_news.Title)
                    {
                        Id = _news.Id,
                        NewsOrganisationId = _news.NewsOrganisationId,
                        NewsOrganisationName = _news.NewsOrganisationName
                    };
                }

                return _news;
            });

        // Act
        var result = await Attach(podcast.Id, episode.Id, ContentKind.NewsReport, news.Id);

        // Assert
        result.Status.Should().Be(EpisodeYouTubeAlternateAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        _episodes.GetStored(episode.Id).Should().NotBeNull();
        _uploads.Should().HaveCountGreaterThan(1);
    }
}
