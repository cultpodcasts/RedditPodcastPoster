using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
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
    private const string SyntheticTweetId = "1850000000000000001";
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TweetIdRules()
    {
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<string>()))
            .Returns((string name) => name);
    }

    // ---- reading X responses ------------------------------------------------------------------

    [Fact(DisplayName = "When X accepts a post, then its id is read from the response, because un-tweet deletes by that id.")]
    public void posted_tweet_id_is_read()
    {
        // Arrange
        var body = $"{{\"data\":{{\"id\":\"{SyntheticTweetId}\",\"text\":\"synthetic\",\"edit_history_tweet_ids\":[\"{SyntheticTweetId}\"]}}}}";

        // Act
        var id = TweetResponseReader.ReadPostedTweetId(body);

        // Assert
        id.Should().Be(SyntheticTweetId);
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

    [Theory(DisplayName = "Delete responses map to Deleted only on deleted=true; a 404 or resource-not-found is NotFound; anything else is a failure.")]
    [InlineData(HttpStatusCode.OK, "{\"data\":{\"deleted\":true}}", DeleteTweetState.Deleted)]
    [InlineData(HttpStatusCode.OK, "{\"data\":{\"deleted\":false}}", DeleteTweetState.Failed)]
    [InlineData(HttpStatusCode.OK, "", DeleteTweetState.Failed)]
    [InlineData(HttpStatusCode.OK, "{\"errors\":[{\"type\":\"https://api.twitter.com/2/problems/resource-not-found\"}]}", DeleteTweetState.NotFound)]
    [InlineData(HttpStatusCode.OK, "{\"errors\":[{\"type\":\"https://api.x.com/2/problems/resource-not-found\"}]}", DeleteTweetState.NotFound)]
    [InlineData(HttpStatusCode.NotFound, "", DeleteTweetState.NotFound)]
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

    [Fact(DisplayName = "When a podcast episode tweet is sent, then the episode is saved with the new tweet id.")]
    public async Task sent_tweet_stores_the_id_on_the_episode()
    {
        // Arrange
        var item = PodcastItem();
        SetupSend(item, new PostTweetResponse(TweetSendStatus.Sent, TweetId: SyntheticTweetId));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, null);

        // Assert
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Tweeted && e.TweetId == SyntheticTweetId)), Times.Once);
    }

    [Fact(DisplayName = "When X refuses a post as duplicate content, then the episode is marked tweeted and keeps any stored id, because there is no new post id.")]
    public async Task duplicate_keeps_the_stored_id()
    {
        // Arrange
        const string existing = "1850000000000000002";
        var item = PodcastItem(e => e.TweetId = existing);
        SetupSend(item, new PostTweetResponse(TweetSendStatus.DuplicateForbidden, "tweet"));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, null);

        // Assert
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Tweeted && e.TweetId == existing)), Times.Once);
    }

    [Fact(DisplayName = "When a tweet is sent but X's response had no id, then the episode is still marked tweeted, because the post was made.")]
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
            .Verify(x => x.Save(It.Is<Episode>(e => e.Tweeted && e.TweetId == null)), Times.Once);
    }

    [Fact(DisplayName = "When a stored document has no tweetId, then it reads as null; a stored tweetId round-trips, so no Cosmos migration is needed.")]
    public void tweet_id_is_optional_in_json()
    {
        // Arrange
        var withoutId = JsonSerializer.Serialize(new Episode { Title = "synthetic", Tweeted = true });
        var legacy = withoutId.Replace(",\"tweetId\":null", string.Empty);
        var withId = JsonSerializer.Serialize(new Film("Synthetic Film") { Tweeted = true, TweetId = SyntheticTweetId });

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(legacy)!;
        var film = JsonSerializer.Deserialize<Film>(withId)!;

        // Assert
        legacy.Should().NotContain("tweetId");
        episode.Tweeted.Should().BeTrue();
        episode.TweetId.Should().BeNull();
        film.TweetId.Should().Be(SyntheticTweetId);
    }

    [Fact(DisplayName = "ClearTweetState clears both the flag and the id.")]
    public void clear_tweet_state_clears_flag_and_id()
    {
        // Arrange
        var episode = new Episode { Tweeted = true, TweetId = SyntheticTweetId };
        var film = new Film("Synthetic Film") { Tweeted = true, TweetId = SyntheticTweetId };

        // Act
        episode.ClearTweetState();
        film.ClearTweetState();

        // Assert
        episode.Tweeted.Should().BeFalse();
        episode.TweetId.Should().BeNull();
        film.Tweeted.Should().BeFalse();
        film.TweetId.Should().BeNull();
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
        var item = PodcastItem(e => e.TweetId = SyntheticTweetId);
        _mocker.GetMock<ITwitterClient>().Setup(x => x.DeleteTweetById(SyntheticTweetId)).ReturnsAsync(deleteState);
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(expected);
        _mocker.GetMock<ITwitterClient>().Verify(
            x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<string?>()), Times.Never);
        _mocker.GetMock<ITwitterClient>().Verify(x => x.GetTweets(), Times.Never);
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
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), null))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [other], "page-2"));
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), "page-2"))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [match]));
        client.Setup(x => x.DeleteTweetById(match.Id)).ReturnsAsync(DeleteTweetState.Deleted);
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Deleted);
        var expectedStart = new DateTimeOffset(DateTime.SpecifyKind(item.Episode.ReleaseUtc, DateTimeKind.Utc).AddDays(-1));
        client.Verify(x => x.GetTimelineSince(expectedStart, null), Times.Once);
        client.Verify(x => x.DeleteTweetById(other.Id), Times.Never);
    }

    [Fact(DisplayName = "Without a stored id, a timeline read to its end with no match is NotFound, because the whole window since release was checked.")]
    public async Task fallback_whole_window_without_match_is_not_found()
    {
        // Arrange
        var item = PodcastItem();
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), null))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, []));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.NotFound);
        _mocker.GetMock<ITwitterClient>().Verify(x => x.DeleteTweetById(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "Without a stored id, hitting the page cap with more pages left is Other, not NotFound, so the item stays marked tweeted.")]
    public async Task fallback_page_cap_is_not_not_found()
    {
        // Arrange
        var item = PodcastItem();
        var client = _mocker.GetMock<ITwitterClient>();
        client.Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<string?>()))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [], "more"));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Other);
        client.Verify(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), It.IsAny<string?>()),
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
            .Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), null))
            .ReturnsAsync(new GetTweetsResponseWrapper(state));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(expected);
        _mocker.GetMock<ITwitterClient>().Verify(x => x.DeleteTweetById(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "Without a stored id, two matching tweets are ambiguous: nothing is deleted.")]
    public async Task fallback_two_matches_deletes_nothing()
    {
        // Arrange
        var item = PodcastItem();
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), null))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved,
                [TweetFor(item, "1850000000000000020"), TweetFor(item, "1850000000000000021")]));
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Other);
        _mocker.GetMock<ITwitterClient>().Verify(x => x.DeleteTweetById(It.IsAny<string>()), Times.Never);
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
            .Setup(x => x.GetTimelineSince(It.IsAny<DateTimeOffset>(), null))
            .ReturnsAsync(new GetTweetsResponseWrapper(GetTweetsState.Retrieved, [tweet]));
        _mocker.GetMock<ITwitterClient>().Setup(x => x.DeleteTweetById(tweet.Id)).ReturnsAsync(DeleteTweetState.Deleted);
        var sut = _mocker.CreateInstance<TweetManager>();

        // Act
        var result = await sut.RemoveTweet(item);

        // Assert
        result.Should().Be(RemoveTweetState.Deleted);
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
            e.TweetId = null;
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
            .Setup(x => x.Send("tweet"))
            .ReturnsAsync(response);
    }
}
