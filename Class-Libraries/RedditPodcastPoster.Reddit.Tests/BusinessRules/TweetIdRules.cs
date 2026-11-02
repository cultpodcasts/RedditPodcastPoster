using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Templates;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

/// <summary>
/// #1058: the X post id is stored at post time and un-tweet deletes by it. #1204: the timeline fallback for items
/// without an id reads the window since release, and only reports NotFound once the whole window was read.
/// All ids and texts are synthetic; no X call is made.
/// </summary>
public class TweetIdRules
{
    private const string SyntheticTweetPost = "1850000000000000001";
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private const int TweetDays = 7;

    public TweetIdRules()
    {
        _mocker.Use(Options.Create(new PostingCriteria
        {
            MinimumDuration = TimeSpan.FromMinutes(1),
            TweetDays = TweetDays,
            RedditDays = 7,
            BlueSkyDays = 7,
            CategoriserDays = 7
        }));
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<string>()))
            .Returns((string name) => name);
    }

    // ---- reading X responses ------------------------------------------------------------------

    [Fact(DisplayName = "When X accepts a post, then its id is read from the response, because un-tweet deletes by that id.")]
    public void posted_tweet_id_is_read()
    {
        // Arrange
        var body = $"{{\"data\":{{\"id\":\"{SyntheticTweetPost}\",\"text\":\"synthetic\",\"edit_history_tweet_ids\":[\"{SyntheticTweetPost}\"]}}}}";

        // Act
        var id = TweetResponseReader.ReadPostedTweetId(body);

        // Assert
        id.Should().Be(SyntheticTweetPost);
    }

    [Theory(DisplayName = "When the post response has no usable id, then no id is read and nothing throws, because the post was still made.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"data\":{\"id\":\"abc\"}}")]
    [InlineData("{\"data\":{\"id\":\"12345678901234567890\"}}")]
    public void bad_post_response_reads_no_id(string? body)
    {
        // Arrange
        var responseBody = body;

        // Act
        var id = TweetResponseReader.ReadPostedTweetId(responseBody);

        // Assert
        id.Should().BeNull();
    }

    [Theory(DisplayName = "Delete responses map to Deleted only on deleted=true; NotFound needs X to report not-found (a bare 404 is a failure); anything else is a failure.")]
    [InlineData(HttpStatusCode.OK, "{\"data\":{\"deleted\":true}}", DeleteTweetState.Deleted)]
    [InlineData(HttpStatusCode.OK, "{\"data\":{\"deleted\":false}}", DeleteTweetState.Failed)]
    [InlineData(HttpStatusCode.OK, "", DeleteTweetState.Failed)]
    [InlineData(HttpStatusCode.OK, "{\"errors\":[{\"type\":\"https://api.twitter.com/2/problems/resource-not-found\"}]}", DeleteTweetState.NotFound)]
    [InlineData(HttpStatusCode.OK, "{\"errors\":[{\"type\":\"https://api.x.com/2/problems/resource-not-found\"}]}", DeleteTweetState.NotFound)]
    [InlineData(HttpStatusCode.NotFound, "", DeleteTweetState.Failed)]
    [InlineData(HttpStatusCode.NotFound, "<html><body>Not Found</body></html>", DeleteTweetState.Failed)]
    [InlineData(HttpStatusCode.NotFound, "{\"type\":\"https://api.x.com/2/problems/resource-not-found\",\"title\":\"Not Found Error\"}", DeleteTweetState.NotFound)]
    [InlineData(HttpStatusCode.NotFound, "{\"title\":\"Not Found Error\",\"detail\":\"synthetic\"}", DeleteTweetState.NotFound)]
    [InlineData(HttpStatusCode.NotFound, "{\"errors\":[{\"type\":\"https://api.twitter.com/2/problems/resource-not-found\"}]}", DeleteTweetState.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, "", DeleteTweetState.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden, "{\"errors\":[{\"type\":\"https://api.twitter.com/2/problems/resource-not-found\"}]}", DeleteTweetState.Failed)]
    [InlineData(HttpStatusCode.Unauthorized, "", DeleteTweetState.Failed)]
    public void delete_response_maps(HttpStatusCode status, string body, DeleteTweetState expected)
    {
        // Arrange
        var responseBody = body;

        // Act
        var state = TweetResponseReader.ReadDeleteState(status, responseBody);

        // Assert
        state.Should().Be(expected);
    }

    // ---- storing the id -------------------------------------------------------------------------

    [Fact(DisplayName = "When a podcast episode tweet is sent, then the episode is saved with tweetPost set and the legacy tweeted flag left unset.")]
    public async Task sent_tweet_stores_the_id_on_the_episode()
    {
        // Arrange
        var item = PodcastItem();
        SetupSend(item, new PostTweetResponse(TweetSendStatus.Sent, TweetId: SyntheticTweetPost));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, null);

        // Assert
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Tweeted && e.TweetPost == SyntheticTweetPost && e.OldTweeted != true)), Times.Once);
    }

    [Fact(DisplayName = "When X refuses a post as duplicate content, then the episode is marked tweeted and keeps any stored id, because there is no new post id.")]
    public async Task duplicate_keeps_the_stored_id()
    {
        // Arrange
        const string existing = "1850000000000000002";
        var item = PodcastItem(e => e.TweetPost = existing);
        SetupSend(item, new PostTweetResponse(TweetSendStatus.DuplicateForbidden, "tweet"));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, null);

        // Assert
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Tweeted && e.TweetPost == existing && e.OldTweeted != true)), Times.Once);
    }

    [Fact(DisplayName = "When a tweet is sent but X's response had no id, then the legacy tweeted flag is set, because the post was made and there is no id to store.")]
    public async Task sent_without_id_still_marks_tweeted()
    {
        // Arrange
        var item = PodcastItem();
        SetupSend(item, new PostTweetResponse(TweetSendStatus.Sent));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, null);

        // Assert
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Tweeted && e.TweetPost == null && e.OldTweeted == true)), Times.Once);
    }

    [Fact(DisplayName = "When a document stores tweetPost, then it reads as posted and writes tweetPost; a short-lived tweetId is read into tweetPost and not written back, and a legacy tweeted flag without an id still counts as posted.")]
    public void tweet_post_matches_the_bluesky_document_shape()
    {
        // Arrange
        var legacyFlag = """{"title":"synthetic","tweeted":true}""";
        var shortLivedId = $$"""{"name":"Synthetic Film","tweetId":"{{SyntheticTweetPost}}"}""";
        var storedPost = JsonSerializer.Serialize(new Film("Synthetic Film") { TweetPost = SyntheticTweetPost });

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(legacyFlag)!;
        var fromShortLived = JsonSerializer.Deserialize<Film>(shortLivedId)!;
        var rewritten = JsonSerializer.Serialize(fromShortLived);
        var film = JsonSerializer.Deserialize<Film>(storedPost)!;

        // Assert
        episode.Tweeted.Should().BeTrue();
        episode.TweetPost.Should().BeNull();
        episode.OldTweeted.Should().BeTrue();
        fromShortLived.Tweeted.Should().BeTrue();
        fromShortLived.TweetPost.Should().Be(SyntheticTweetPost);
        rewritten.Should().NotContain("tweetId");
        rewritten.Should().Contain("tweetPost");
        storedPost.Should().NotContain("tweetId");
        storedPost.Should().NotContain("\"tweeted\":true");
        film.TweetPost.Should().Be(SyntheticTweetPost);
        film.OldTweeted.Should().BeNull();
    }

    [Fact(DisplayName = "ClearTweetState clears both the flag and the id.")]
    public void clear_tweet_state_clears_flag_and_id()
    {
        // Arrange
        var episode = new Episode { OldTweeted = true, TweetPost = SyntheticTweetPost };
        var film = new Film("Synthetic Film") { OldTweeted = true, TweetPost = SyntheticTweetPost };

        // Act
        episode.ClearTweetState();
        film.ClearTweetState();

        // Assert
        episode.Tweeted.Should().BeFalse();
        episode.TweetPost.Should().BeNull();
        film.Tweeted.Should().BeFalse();
        film.TweetPost.Should().BeNull();
    }

    // ---- removing by id -------------------------------------------------------------------------

    [Theory(DisplayName = "With a stored tweet id, un-tweet deletes by that id and never reads the timeline.")]
    [InlineData(DeleteTweetState.Deleted, RemoveTweetState.Deleted)]
    [InlineData(DeleteTweetState.NotFound, RemoveTweetState.NotFound)]
    [InlineData(DeleteTweetState.TooManyRequests, RemoveTweetState.TooManyRequests)]
    [InlineData(DeleteTweetState.Failed, RemoveTweetState.Other)]
    public async Task stored_id_deletes_by_id(DeleteTweetState deleteState, RemoveTweetState expected)
    {
        // Arrange
        var item = PodcastItem(e => e.TweetPost = SyntheticTweetPost);
        _mocker.GetMock<ITwitterClient>().Setup(x => x.DeleteTweetById(SyntheticTweetPost, It.IsAny<CancellationToken>())).ReturnsAsync(deleteState);
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(expected);
        _mocker.GetMock<ITwitterClient>().Verify(
            x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ITwitterClient>().Verify(x => x.GetTweets(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- timeline fallback ----------------------------------------------------------------------

    [Fact(DisplayName = "Without a stored id, un-tweet reads the timeline from the day before release, follows the next page, and deletes the match by its id.")]
    public async Task fallback_pages_and_deletes_the_match()
    {
        // Arrange
        var item = PodcastItem();
        var match = TweetFor(item, "1850000000000000010");
        var other = new Tweet { Id = "1850000000000000011", Text = "\"Unrelated\"\nSomeone Else\n1 Jan 2001 [0:01:00]" };
        var client = _mocker.GetMock<ITwitterClient>();
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [other], "page-2"));
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), "page-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [match]));
        client.Setup(x => x.DeleteTweetById(match.Id, It.IsAny<CancellationToken>())).ReturnsAsync(DeleteTweetState.Deleted);
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Deleted);
        var expectedStart = new DateTimeOffset(DateTime.SpecifyKind(item.Episode.ReleaseUtc, DateTimeKind.Utc).AddDays(-1));
        client.Verify(x => x.GetTimelineSince(expectedStart, null, null, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.DeleteTweetById(other.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Without a stored id, a timeline read to its end with no match is NotFound, because the whole window since release was checked.")]
    public async Task fallback_whole_window_without_match_is_not_found()
    {
        // Arrange
        var item = PodcastItem();
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, []));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.NotFound);
        _mocker.GetMock<ITwitterClient>().Verify(x => x.DeleteTweetById(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Without a stored id, hitting the page cap with more pages left is Other, not NotFound, so the item stays marked tweeted.")]
    public async Task fallback_page_cap_is_not_not_found()
    {
        // Arrange
        var item = PodcastItem();
        var client = _mocker.GetMock<ITwitterClient>();
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [], "more"));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Other);
        client.Verify(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(TweetManager.MaxTimelinePages));
    }

    [Theory(DisplayName = "Without a stored id, a rate-limited or failed timeline read is passed on and nothing is deleted.")]
    [InlineData(GetTweetsState.TooManyRequests, RemoveTweetState.TooManyRequests)]
    [InlineData(GetTweetsState.Other, RemoveTweetState.Other)]
    public async Task fallback_read_failure(GetTweetsState state, RemoveTweetState expected)
    {
        // Arrange
        var item = PodcastItem();
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(state));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(expected);
        _mocker.GetMock<ITwitterClient>().Verify(x => x.DeleteTweetById(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Without a stored id, two matching tweets are ambiguous: nothing is deleted.")]
    public async Task fallback_two_matches_deletes_nothing()
    {
        // Arrange
        var item = PodcastItem();
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved,
                [TweetFor(item, "1850000000000000020"), TweetFor(item, "1850000000000000021")]));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Other);
        _mocker.GetMock<ITwitterClient>().Verify(x => x.DeleteTweetById(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Without a stored id, the timeline match uses the sanitised podcast name, because that is what the tweet builder writes.")]
    public async Task fallback_matches_the_sanitised_name()
    {
        // Arrange
        var item = PodcastItem(customisePodcast: p => p.Name = "Synthetic  Cult’s Show");
        const string posted = "Synthetic Cult's Show";
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(item.Podcast.Name))
            .Returns(posted);
        var tweet = new Tweet
        {
            Id = "1850000000000000030",
            Text = $"\"Synthetic title\"\n{posted}\n{SocialPostKindTemplate.ReleaseLine(item)}\nhttps://t.co/synthetic"
        };
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [tweet]));
        _mocker.GetMock<ITwitterClient>().Setup(x => x.DeleteTweetById(tweet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(DeleteTweetState.Deleted);
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Deleted);
    }

    [Fact(DisplayName = "Without a stored id, a match on page 1 and another on page 2 is ambiguous: nothing is deleted.")]
    public async Task fallback_matches_across_pages_delete_nothing()
    {
        // Arrange
        var item = PodcastItem();
        var client = _mocker.GetMock<ITwitterClient>();
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved,
                [TweetFor(item, "1850000000000000040")], "page-2"));
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), "page-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [TweetFor(item, "1850000000000000041")]));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Other);
        client.Verify(x => x.DeleteTweetById(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Without a stored id, a single match is deleted only after the rest of the window is read, so a second copy would be caught.")]
    public async Task fallback_reads_the_rest_of_the_window_before_deleting()
    {
        // Arrange
        var item = PodcastItem();
        var match = TweetFor(item, "1850000000000000050");
        var client = _mocker.GetMock<ITwitterClient>();
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [match], "page-2"));
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), "page-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, []));
        client.Setup(x => x.DeleteTweetById(match.Id, It.IsAny<CancellationToken>())).ReturnsAsync(DeleteTweetState.Deleted);
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Deleted);
        client.Verify(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), "page-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Without a stored id, a single match with pages left at the cap is deleted but reported as Other, so the item stays tweeted.")]
    public async Task fallback_single_match_at_cap_is_other()
    {
        // Arrange
        var item = PodcastItem();
        var match = TweetFor(item, "1850000000000000060");
        var client = _mocker.GetMock<ITwitterClient>();
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [match], "more"));
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), "more", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [], "more"));
        client.Setup(x => x.DeleteTweetById(match.Id, It.IsAny<CancellationToken>())).ReturnsAsync(DeleteTweetState.Deleted);
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Other);
        client.Verify(x => x.DeleteTweetById(match.Id, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(TweetManager.MaxTimelinePages));
    }

    [Fact(DisplayName = "Without a stored id, an older podcast episode's search is bounded by end_time, and no match in that window is Other, because a later hand-made post is possible.")]
    public async Task fallback_bounded_window_without_match_is_other()
    {
        // Arrange
        var item = PodcastItem(e => e.ReleaseUtc = DomainTestFixture.UtcAtTime(-30, TimeSpan.FromHours(9)));
        var expectedEnd = new DateTimeOffset(item.Episode.ReleaseUtc.AddDays(TweetDays + TweetManager.EndTimeSlackDays), TimeSpan.Zero);
        var client = _mocker.GetMock<ITwitterClient>();
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, []));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Other);
        client.Verify(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), expectedEnd, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "end_time is release + tweet-days + slack for an older podcast episode, and absent for a recent one, a catalogue item, or unset tweet-days.")]
    public void timeline_end()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var old = PodcastItem(e => e.ReleaseUtc = DomainTestFixture.UtcAtTime(-30, TimeSpan.FromHours(9)));
        var recent = PodcastItem(e => e.ReleaseUtc = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(9)));
        var tv = CatalogueParentKindMapper.ToPostProjection(
            new TvShowEpisode("Synthetic TV episode") { Id = _fixture.CreateGuid() },
            new TvShow("Synthetic TV show") { Id = _fixture.CreateGuid() });
        tv.Episode.ReleaseUtc = DomainTestFixture.UtcAtTime(-30, TimeSpan.FromHours(9));

        // Act
        var oldEnd = TweetManager.TimelineEnd(old, TweetDays, now);
        var recentEnd = TweetManager.TimelineEnd(recent, TweetDays, now);
        var tvEnd = TweetManager.TimelineEnd(tv, TweetDays, now);
        var unsetEnd = TweetManager.TimelineEnd(old, 0, now);

        // Assert
        oldEnd.Should().Be(new DateTimeOffset(old.Episode.ReleaseUtc.AddDays(TweetDays + TweetManager.EndTimeSlackDays), TimeSpan.Zero));
        recentEnd.Should().BeNull();
        tvEnd.Should().BeNull();
        unsetEnd.Should().BeNull();
    }

    [Fact(DisplayName = "When a new tweet is sent for an item that already had a different tweet id, then the new id is stored and a warning names both ids.")]
    public async Task sent_over_an_existing_id_warns()
    {
        // Arrange
        const string previous = "1850000000000000070";
        var item = PodcastItem(e => e.TweetPost = previous);
        SetupSend(item, new PostTweetResponse(TweetSendStatus.Sent, TweetId: SyntheticTweetPost));
        var logger = _mocker.GetMock<ILogger<TweetPoster>>();
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, null);

        // Assert
        item.Episode.TweetPost.Should().Be(SyntheticTweetPost);
        logger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) =>
                state.ToString()!.Contains(previous) && state.ToString()!.Contains(SyntheticTweetPost)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact(DisplayName = "The timeline window starts a day before release, and never before X's earliest start_time.")]
    public void timeline_start()
    {
        // Arrange
        var release = DomainTestFixture.UtcAtTime(-3, TimeSpan.FromHours(9));

        // Act
        var start = TweetManager.TimelineStart(release);
        var floor = TweetManager.TimelineStart(DateTime.MinValue);

        // Assert
        start.Should().Be(new DateTimeOffset(release.AddDays(-1), TimeSpan.Zero));
        floor.Should().Be(TweetManager.EarliestStartTime);
    }

    [Fact(DisplayName = "A podcast post written with the invariant month name still matches when removed under a culture with a different one.")]
    public void fingerprint_matches_either_culture()
    {
        // Arrange
        // September: en-GB writes "Sept", the invariant culture "Sep".
        var september = Enumerable.Range(1, 366)
            .Select(days => DomainTestFixture.UtcAtTime(-days, TimeSpan.FromHours(8)))
            .First(date => date.Month == 9);
        var item = PodcastItem(e => e.ReleaseUtc = september);
        var invariantLine =
            $"{item.Episode.ReleaseUtc.ToString(SocialPostKindTemplate.ReleaseFormat, CultureInfo.InvariantCulture)} " +
            item.Episode.Length.ToString(SocialPostKindTemplate.LengthFormat, CultureInfo.InvariantCulture);
        var text = $"\"Synthetic title\"\n{item.Podcast.Name}\n{invariantLine}";
        var original = CultureInfo.CurrentCulture;
        bool matches;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            var fingerprint = SocialPostRemovalFingerprint.For(item);

            // Act
            matches = fingerprint.Matches(text);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        // Assert
        matches.Should().BeTrue();
    }

    private PodcastEpisode PodcastItem(Action<Episode>? customiseEpisode = null, Action<Podcast>? customisePodcast = null)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = "Synthetic Cult Podcast";
            customisePodcast?.Invoke(p);
        });
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.ReleaseUtc = DomainTestFixture.UtcDaysAgo(2).AddHours(9);
            e.Length = TimeSpan.FromMinutes(61);
            e.TweetPost = null;
            customiseEpisode?.Invoke(e);
        });
        return new PodcastEpisode(podcast, episode);
    }

    private static Tweet TweetFor(PodcastEpisode item, string id) => new()
    {
        Id = id,
        Text = $"\"Synthetic title\"\n{item.Podcast.Name}\n{SocialPostKindTemplate.ReleaseLine(item)}\nhttps://t.co/synthetic"
    };

    private void SetupSend(PodcastEpisode item, PostTweetResponse response)
    {
        _mocker.GetMock<ITweetBuilder>()
            .Setup(x => x.BuildTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync("tweet");
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.Send("tweet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }
}
