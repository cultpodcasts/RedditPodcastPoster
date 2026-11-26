using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.News;
using FunctionHost.Tests.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

public class NewsReportYouTubeAlternateUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private NewsReport? _saved;

    public NewsReportYouTubeAlternateUpdateRules()
    {
        _mocker.Use(NullLogger<NewsReportService>.Instance);
        _mocker.Use(NullLogger<CataloguePlayableSearchSync>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mocker.Use(_mocker.CreateInstance<CataloguePlayableSearchSync>());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(report => _saved = report)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.TryReplace(It.IsAny<NewsReport>(), It.IsAny<string>()))
            .Callback<NewsReport, string>((report, _) => _saved = report)
            .ReturnsAsync(true);
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(lookup => lookup.FindYouTubeHolderExcluding(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogueServiceUrlOwner?)null);
    }

    private NewsReport CreateReport(Action<NewsReport>? customise = null)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Length = _fixture.CreateDuration()
        };
        report.SetNewsOrganisationProperties(organisation);
        report.SetRelease(CatalogueRelease.FromDate(DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(10))));
        customise?.Invoke(report);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<System.Linq.Expressions.Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync(report);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetNewsReportWithETag(report.NewsOrganisationId, report.Id))
            .ReturnsAsync(() => new ETagged<NewsReport>(report, "\"synthetic-etag\""));
        return report;
    }

    private YouTubeAlternate CreateAlternate()
    {
        var id = _fixture.CreateYouTubeId();
        return new YouTubeAlternate
        {
            YouTubeId = id,
            Url = _fixture.DefaultYouTubeUrl(id),
            ChannelName = _fixture.CreateTitle(),
            AddedAt = DateTime.UtcNow
        };
    }

    private static NewsReportChangeRequest SeenYouTube(NewsReport report, NewsReportChangeRequest change)
    {
        change.ExpectedAdditionalYouTubeIds = report.AdditionalYouTube?.Select(item => item.YouTubeId).ToArray() ?? [];
        return change;
    }

    private Task<NewsReportUpdateResult> Update(NewsReport report, NewsReportChangeRequest change) =>
        _mocker.CreateInstance<NewsReportService>()
            .UpdateAsync(new NewsReportChangeRequestWrapper(report.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH news report additionalYouTube adds a YouTube URL as an alternate, keeps listed existing ids, and a keep-list that omits an id removes it, because news reports share the film additional-YouTube edit (RPP #1526).")]
    public async Task additional_youtube_add_keep_and_remove()
    {
        // Arrange
        var existing = CreateAlternate();
        var report = CreateReport(r => r.AdditionalYouTube = [existing]);
        var addedId = _fixture.CreateYouTubeId();
        var channelName = _fixture.CreateTitle();

        // Act
        var added = await Update(report, SeenYouTube(report, new NewsReportChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(existing.YouTubeId, null, null, null),
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(addedId).ToString(), _fixture.CreateTitle(), channelName)
            ]
        }));

        // Assert
        added.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _saved!.AdditionalYouTube.Should().HaveCount(2);
        _saved.AdditionalYouTube![0].YouTubeId.Should().Be(existing.YouTubeId);
        _saved.AdditionalYouTube[1].YouTubeId.Should().Be(addedId);
        _saved.AdditionalYouTube[1].ChannelName.Should().Be(channelName);

        // Arrange
        report.AdditionalYouTube = _saved.AdditionalYouTube;

        // Act
        var removed = await Update(report, SeenYouTube(report, new NewsReportChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(existing.YouTubeId, null, null, null)]
        }));

        // Assert
        removed.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _saved.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(existing.YouTubeId);
    }

    [Fact(DisplayName =
        "PATCH news report promoteYouTubeId swaps that additional upload into the primary YouTube link and compact youTubeId, and the previous primary becomes the first additional upload (RPP #1526).")]
    public async Task additional_youtube_promote_swaps_into_primary()
    {
        // Arrange
        var primaryId = _fixture.CreateYouTubeId();
        var alternate = CreateAlternate();
        var report = CreateReport(r =>
        {
            r.YouTubeId = primaryId;
            r.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(primaryId) }
            };
            r.AdditionalYouTube = [alternate];
        });

        // Act
        var result = await Update(report, SeenYouTube(report, new NewsReportChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(alternate.YouTubeId, null, null, null)],
            PromoteYouTubeId = alternate.YouTubeId
        }));

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _saved!.YouTubeId.Should().Be(alternate.YouTubeId);
        _saved.Services!["youtube"].Url.Should().Be(alternate.Url);
        _saved.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(primaryId);
    }

    [Fact(DisplayName =
        "PATCH news report additionalYouTube is a conflict naming the ids the client did not list when the keep-list omits them, and nothing is saved (RPP #1526).")]
    public async Task additional_youtube_keep_list_without_expected_ids_is_conflict()
    {
        // Arrange
        var existing = CreateAlternate();
        var report = CreateReport(r => r.AdditionalYouTube = [existing]);

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(existing.YouTubeId, null, null, null)]
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Conflict);
        result.UnexpectedAdditionalYouTubeIds.Should().Equal(existing.YouTubeId);
        result.Message.Should().Contain("news report");
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news report additionalYouTube cannot add the report's primary YouTube id, because that id is already the playable link (RPP #1526).")]
    public async Task additional_youtube_rejects_primary_id()
    {
        // Arrange
        var primaryId = _fixture.CreateYouTubeId();
        var report = CreateReport(r =>
        {
            r.YouTubeId = primaryId;
            r.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(primaryId) }
            };
        });

        // Act
        var result = await Update(report, SeenYouTube(report, new NewsReportChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(primaryId).ToString(), null, null)
            ]
        }));

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.BadRequest);
        result.Message.Should().Contain(primaryId);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news report promoteYouTubeId keeps the old primary as additionalYouTube[0] when the report has youTubeId but no services.youtube url, and when the stored url cannot be parsed (RPP #1526).")]
    public async Task additional_youtube_promote_keeps_old_primary_when_url_is_missing_or_unparseable()
    {
        // Arrange
        var primaryId = _fixture.CreateYouTubeId();
        var alternate = CreateAlternate();
        var report = CreateReport(r =>
        {
            r.YouTubeId = primaryId;
            r.Services = null;
            r.AdditionalYouTube = [alternate];
        });

        // Act
        var missingUrl = await Update(report, SeenYouTube(report, new NewsReportChangeRequest
        {
            PromoteYouTubeId = alternate.YouTubeId
        }));
        var kept = _saved!.AdditionalYouTube!.ToList();

        var unparseable = CreateReport(r =>
        {
            r.YouTubeId = primaryId;
            r.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri("https://example.com/not-youtube") }
            };
            r.AdditionalYouTube = [alternate];
        });
        var unparseableResult = await Update(unparseable, SeenYouTube(unparseable, new NewsReportChangeRequest
        {
            PromoteYouTubeId = alternate.YouTubeId
        }));

        // Assert
        missingUrl.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        kept[0].YouTubeId.Should().Be(primaryId);
        kept[0].Url.Should().Be(_fixture.DefaultYouTubeUrl(primaryId));
        unparseableResult.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _saved!.AdditionalYouTube!.First().YouTubeId.Should().Be(primaryId);
        _saved.AdditionalYouTube!.First().Url.Should().Be(_fixture.DefaultYouTubeUrl(primaryId));
        _mocker.GetMock<INewsReportRepository>().Verify(
            r => r.TryReplace(It.IsAny<NewsReport>(), It.IsAny<string>()), Times.Exactly(2));
        _mocker.GetMock<INewsReportRepository>().Verify(r => r.Save(It.IsAny<NewsReport>()), Times.Never);
    }

    [Fact(DisplayName =
        "PATCH news report additionalYouTube is a bad request when it lists the YouTube id that services.youtube is being changed to in the same request, because the resulting primary cannot also be an alternate (RPP #1526).")]
    public async Task additional_youtube_rejects_id_that_becomes_the_primary_in_the_same_patch()
    {
        // Arrange
        var newPrimary = _fixture.CreateYouTubeId();
        var report = CreateReport();

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(newPrimary) }
            },
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(newPrimary).ToString(), null, null)
            ]
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.BadRequest);
        result.Message.Should().Contain(newPrimary);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news report additionalYouTube is a conflict naming the holder when the added YouTube id already belongs to another item (RPP #1526).")]
    public async Task additional_youtube_add_is_conflict_when_another_item_already_holds_the_id()
    {
        // Arrange
        var heldId = _fixture.CreateYouTubeId();
        var holderId = _fixture.CreateGuid();
        var holderName = _fixture.CreateTitle();
        var report = CreateReport();
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(lookup => lookup.FindYouTubeHolderExcluding(
                heldId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueServiceUrlOwner("TvShowEpisode", holderName, holderId));

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(heldId).ToString(), null, null)
            ]
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Conflict);
        result.Message.Should().Contain(heldId);
        result.Message.Should().Contain("TvShowEpisode");
        result.Message.Should().Contain(holderId.ToString());
        result.Message.Should().Contain(holderName);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "Curator GET of a news report includes additionalYouTube, because the editor reloads the list it will PATCH (RPP #1526).")]
    public void curator_get_includes_additional_youtube()
    {
        // Arrange
        var alternate = CreateAlternate();
        var report = CreateReport(r => r.AdditionalYouTube = [alternate]);

        // Act
        var dto = report.ToDto();

        // Assert
        dto.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(alternate.YouTubeId);
        dto.AdditionalYouTube![0].Url.Should().Be(alternate.Url);
    }

    [Fact(DisplayName =
        "PATCH news report additionalYouTube that loses its replace and then sees an unlisted alternate is a conflict, and the tweet and Bluesky posts stay up, because a 409 must not retract a live post (RPP #1526).")]
    public async Task lost_replace_then_stale_keep_list_does_not_retract_social_posts()
    {
        // Arrange
        var alternate = CreateAlternate();
        var extra = CreateAlternate();
        var report = CreateReport(r =>
        {
            r.AdditionalYouTube = [alternate];
            r.OldTweeted = true;
            r.OldBlueskyPosted = true;
        });
        var raced = new NewsReport(report.Title)
        {
            Id = report.Id,
            Length = report.Length,
            OldTweeted = true,
            OldBlueskyPosted = true,
            AdditionalYouTube = [alternate, extra]
        };
        raced.SetNewsOrganisationProperties(new NewsOrganisation(report.NewsOrganisationName ?? _fixture.CreateTitle())
        {
            Id = report.NewsOrganisationId
        });
        var reads = 0;
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetNewsReportWithETag(report.NewsOrganisationId, report.Id))
            .ReturnsAsync(() => new ETagged<NewsReport>(reads++ == 0 ? report : raced, "\"synthetic-etag\""));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.TryReplace(It.IsAny<NewsReport>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(alternate.YouTubeId, null, null, null)],
            ExpectedAdditionalYouTubeIds = [alternate.YouTubeId],
            Tweeted = false,
            BlueskyPosted = false
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Conflict);
        result.UnexpectedAdditionalYouTubeIds.Should().Equal(extra.YouTubeId);
        _saved.Should().BeNull();
        _mocker.GetMock<IPlayableSocialRetraction>().Verify(
            s => s.RemoveTweet(It.IsAny<PostStatusChange>(), It.IsAny<IPromotable>(), It.IsAny<SocialPostRemovalTarget>()),
            Times.Never);
        _mocker.GetMock<IPlayableSocialRetraction>().Verify(
            s => s.RemoveBlueskyPost(It.IsAny<PostStatusChange>(), It.IsAny<IPromotable>(), It.IsAny<SocialPostRemovalTarget>()),
            Times.Never);
    }
}
