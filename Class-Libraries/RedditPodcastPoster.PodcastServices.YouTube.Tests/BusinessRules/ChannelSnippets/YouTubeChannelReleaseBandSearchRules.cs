using System.Net;
using FluentAssertions;
using Google;
using Google.Apis.Requests;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ChannelSnippets;

/// <summary>
/// A submit searches one channel inside a publish band. The search stops after two pages
/// even when YouTube still offers another page, and account-delegation forbidden is not an empty result.
/// </summary>
public class YouTubeChannelReleaseBandSearchRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeChannelReleaseBandSearchRules()
    {
        _mocker.Use(NullLogger<YouTubeChannelReleaseBandSearch>.Instance);
        var application = new Application
        {
            ApiKey = _fixture.Create<string>(),
            Name = _fixture.Create<string>(),
            DisplayName = _fixture.Create<string>()
        };
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.CurrentApplication).Returns(application);
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.Usage).Returns(ApplicationUsage.Api);
        _mocker.GetMock<IYouTubeQuotaUsageTracker>()
            .Setup(x => x.RecordQuotaConsumedAsync(
                It.IsAny<Application>(),
                It.IsAny<ApplicationUsage>(),
                It.IsAny<YouTubeQuotaOperation>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IYouTubeQuotaUsageTracker>()
            .Setup(x => x.RecordNonQuotaErrorAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "When Search.List keeps a next page token, the channel release-band search executes at most two pages, " +
        "passes the band edges, drops live and upcoming videos, and does not fetch a third page " +
        "because a submit looks around one date.")]
    public async Task Channel_search_stops_after_two_pages_inside_the_band()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var publishedAfter = new DateTimeOffset(DomainTestFixture.UtcDateDaysAgo(20));
        var publishedBefore = new DateTimeOffset(DomainTestFixture.UtcDateDaysAgo(1));
        var firstVideoId = _fixture.CreateYouTubeId();
        var secondVideoId = _fixture.CreateYouTubeId();
        var calls = new List<YouTubeChannelSearchPageRequest>();
        _mocker.GetMock<IYouTubeSearchListExecutor>()
            .Setup(x => x.ExecuteAsync(It.IsAny<YouTubeChannelSearchPageRequest>()))
            .ReturnsAsync((YouTubeChannelSearchPageRequest request) =>
            {
                calls.Add(request);
                var items = new List<SearchResult>();
                string pageToken;
                if (calls.Count == 1)
                {
                    pageToken = "page-2";
                    items.Add(CreateSearchResult(firstVideoId, "none"));
                    items.Add(CreateSearchResult(_fixture.CreateYouTubeId(), "live"));
                    items.Add(CreateSearchResult(_fixture.CreateYouTubeId(), "upcoming"));
                }
                else
                {
                    pageToken = "page-3";
                    items.Add(CreateSearchResult(secondVideoId, "none"));
                }

                return new SearchListResponse
                {
                    NextPageToken = pageToken,
                    Items = items
                };
            });
        var sut = _mocker.CreateInstance<YouTubeChannelReleaseBandSearch>();

        // Act
        var result = await sut.Search(channelId, publishedAfter, publishedBefore, new IndexingContext());

        // Assert
        calls.Should().HaveCount(YouTubeChannelReleaseBandSearch.MaxPages);
        calls.Should().OnlyContain(x =>
            x.PublishedAfterDateTimeOffset == publishedAfter &&
            x.PublishedBeforeDateTimeOffset == publishedBefore &&
            x.ChannelId == channelId &&
            x.MaxResults == YouTubeChannelReleaseBandSearch.PageSize);
        calls[0].PageToken.Should().BeEmpty();
        calls[1].PageToken.Should().Be("page-2");
        result.Should().NotBeNull();
        result!.Select(x => x.Id.VideoId).Should().Equal(firstVideoId, secondVideoId);
    }

    [Fact(DisplayName =
        "When Search.List returns account-delegation forbidden, the channel release-band search throws " +
        "YouTubeChannelSearchForbiddenException because an empty list would look like no videos in the band.")]
    public async Task Account_delegation_forbidden_throws()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var forbidden = new GoogleApiException("youtube", "The caller is not permitted.")
        {
            HttpStatusCode = HttpStatusCode.Forbidden,
            Error = new RequestError
            {
                Errors =
                [
                    new SingleError { Reason = "accountDelegationForbidden" }
                ]
            }
        };
        _mocker.GetMock<IYouTubeSearchListExecutor>()
            .Setup(x => x.ExecuteAsync(It.IsAny<YouTubeChannelSearchPageRequest>()))
            .ThrowsAsync(forbidden);
        var sut = _mocker.CreateInstance<YouTubeChannelReleaseBandSearch>();

        // Act
        var act = () => sut.Search(
            channelId,
            new DateTimeOffset(DomainTestFixture.UtcDateDaysAgo(3)),
            new DateTimeOffset(DomainTestFixture.UtcDateDaysAgo(1)),
            new IndexingContext());

        // Assert
        var thrown = await act.Should().ThrowAsync<YouTubeChannelSearchForbiddenException>();
        thrown.Which.ChannelId.Should().Be(channelId);
    }

    [Fact(DisplayName =
        "When Search.List fails for a reason other than quota or account-delegation forbidden, the channel release-band search returns no videos " +
        "because that failure is not proof the channel forbids search.")]
    public async Task Other_google_errors_return_an_empty_page()
    {
        // Arrange
        var failure = new GoogleApiException("youtube", "backendError")
        {
            HttpStatusCode = HttpStatusCode.InternalServerError
        };
        _mocker.GetMock<IYouTubeSearchListExecutor>()
            .Setup(x => x.ExecuteAsync(It.IsAny<YouTubeChannelSearchPageRequest>()))
            .ThrowsAsync(failure);
        var sut = _mocker.CreateInstance<YouTubeChannelReleaseBandSearch>();

        // Act
        var result = await sut.Search(
            _fixture.CreateYouTubeChannelId(),
            new DateTimeOffset(DomainTestFixture.UtcDateDaysAgo(3)),
            new DateTimeOffset(DomainTestFixture.UtcDateDaysAgo(1)),
            new IndexingContext());

        // Assert
        result.Should().BeEmpty();
    }

    private static SearchResult CreateSearchResult(string videoId, string liveBroadcastContent) =>
        new()
        {
            Id = new ResourceId { VideoId = videoId },
            Snippet = new SearchResultSnippet
            {
                LiveBroadcastContent = liveBroadcastContent,
                Title = videoId
            }
        };
}
