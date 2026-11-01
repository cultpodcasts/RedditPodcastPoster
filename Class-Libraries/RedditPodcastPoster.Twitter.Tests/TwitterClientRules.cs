using System.Net;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Tests.Fakes;

namespace RedditPodcastPoster.Twitter.Tests;

public class TwitterClientRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly ScriptedHttpHandler _x = new();
    private readonly TwitterOptions _options = new();
    private readonly string _tweet;

    public TwitterClientRules()
    {
        _options.TwitterId = _fixture.Create<long>();
        _tweet = _fixture.Create<string>();
        _mocker.Use(Options.Create(_options));
        _mocker.Use(new HttpClient(_x) {BaseAddress = _options.ApiBaseAddress});
    }

    [Fact(DisplayName = "A post goes to POST https://api.x.com/2/tweets with the text as JSON.")]
    public async Task send_posts_to_api_x_com()
    {
        // Arrange
        _x.Respond(HttpStatusCode.Created, "{\"data\":{\"id\":\"1\"}}");
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var result = await sut.Send(_tweet);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.Sent);
        _x.Requests.Should().ContainSingle();
        _x.Requests[0].Method.Should().Be(HttpMethod.Post);
        _x.Requests[0].Uri.Should().Be(new Uri("https://api.x.com/2/tweets"));
        _x.Requests[0].Body.Should().Contain(_tweet);
    }

    [Fact(DisplayName =
        "A 403 whose JSON detail says duplicate content is reported as DuplicateForbidden, so the episode is marked tweeted rather than retried.")]
    public async Task forbidden_duplicate_detail_is_duplicate()
    {
        // Arrange
        _x.Respond(HttpStatusCode.Forbidden,
            "{\"detail\":\"You are not permitted to create a Tweet with duplicate content.\",\"type\":\"about:blank\",\"title\":\"Forbidden\",\"status\":403}");
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var result = await sut.Send(_tweet);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.DuplicateForbidden);
    }

    [Fact(DisplayName =
        "A 403 that only mentions duplicate content outside the error fields is not a duplicate, so the send is AuthenticationFailed and the run stops.")]
    public async Task forbidden_without_duplicate_detail_is_authentication_failed()
    {
        // Arrange
        _x.Respond(HttpStatusCode.Forbidden,
            "{\"detail\":\"You are not permitted to perform this action.\",\"title\":\"Forbidden\",\"status\":403,\"note\":\"duplicate content\"}");
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var result = await sut.Send(_tweet);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.AuthenticationFailed);
    }

    [Fact(DisplayName = "A 429 is reported as TooManyRequests even when the reset header is malformed.")]
    public async Task too_many_requests_with_bad_reset_header()
    {
        // Arrange
        _x.Respond(HttpStatusCode.TooManyRequests, "{\"title\":\"Too Many Requests\",\"status\":429}",
            new Dictionary<string, string> {["x-rate-limit-reset"] = _fixture.Create<string>()});
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var result = await sut.Send(_tweet);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.TooManyRequests);
    }

    [Fact(DisplayName =
        "A 403 usage-capped problem with no rate-limit headers is reported as TooManyRequests rather than throwing.")]
    public async Task usage_capped_is_too_many_requests()
    {
        // Arrange
        _x.Respond(HttpStatusCode.Forbidden,
            $"{{\"title\":\"UsageCapExceeded\",\"type\":\"{XApiProblem.UsageCappedType}\",\"status\":429}}");
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var result = await sut.Send(_tweet);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.TooManyRequests);
    }

    [Fact(DisplayName = "A non-JSON error body is a plain failure rather than an exception.")]
    public async Task non_json_error_is_failed()
    {
        // Arrange
        _x.Respond(HttpStatusCode.BadGateway, "<html>bad gateway</html>");
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var result = await sut.Send(_tweet);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.Failed);
    }

    [Fact(DisplayName = "Recent posts are read from the configured account's timeline on api.x.com.")]
    public async Task get_tweets_reads_account_timeline()
    {
        // Arrange
        var id = _fixture.Create<string>();
        _x.Respond(HttpStatusCode.OK, $"{{\"data\":[{{\"id\":\"{id}\",\"text\":\"t\"}}],\"meta\":{{}}}}");
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var result = await sut.GetTweets();

        // Assert
        result.State.Should().Be(GetTweetsState.Retrieved);
        result.Tweets!.Single().Id.Should().Be(id);
        _x.Requests[0].Uri.Should()
            .Be(new Uri($"https://api.x.com/2/users/{_options.TwitterId}/tweets?tweet.fields=entities"));
    }

    [Fact(DisplayName = "Deleting a post calls DELETE /2/tweets/{id} and reports success.")]
    public async Task delete_tweet_calls_delete()
    {
        // Arrange
        var tweet = new Tweet {Id = _fixture.Create<string>()};
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var deleted = await sut.DeleteTweet(tweet);

        // Assert
        deleted.Should().BeTrue();
        _x.Requests[0].Method.Should().Be(HttpMethod.Delete);
        _x.Requests[0].Uri.Should().Be(new Uri($"https://api.x.com/2/tweets/{tweet.Id}"));
    }

    [Fact(DisplayName =
        "A timeline read since a release goes to the typed api.x.com client with start_time, end_time and an escaped pagination token, because un-tweet must follow the same auth as posting.")]
    public async Task timeline_since_uses_typed_client_query()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 3, 1, 4, 5, 6, TimeSpan.Zero);
        var end = start.AddDays(2);
        var token = "page token/next";
        _x.Respond(HttpStatusCode.OK, "{\"data\":[],\"meta\":{}}");
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var result = await sut.GetTimelineSince(start, end, token);

        // Assert
        result.State.Should().Be(GetTweetsState.Retrieved);
        _x.Requests.Should().ContainSingle();
        _x.Requests[0].Method.Should().Be(HttpMethod.Get);
        _x.Requests[0].Uri.Should().Be(new Uri(
            "https://api.x.com/2/users/" + _options.TwitterId +
            "/tweets?tweet.fields=entities&max_results=100" +
            "&start_time=2026-03-01T04%3A05%3A06Z" +
            "&end_time=2026-03-03T04%3A05%3A06Z" +
            "&pagination_token=" + Uri.EscapeDataString(token)));
    }

    [Fact(DisplayName =
        "Deleting a post by id uses DELETE /2/tweets/{id} on the typed client, so OAuth 2.0 signs the un-tweet the same way it signs a post.")]
    public async Task delete_by_id_uses_typed_client()
    {
        // Arrange
        var tweetId = _fixture.Create<long>().ToString();
        _x.Respond(HttpStatusCode.OK, "{\"data\":{\"deleted\":true}}");
        var sut = _mocker.CreateInstance<TwitterClient>();

        // Act
        var state = await sut.DeleteTweetById(tweetId);

        // Assert
        state.Should().Be(DeleteTweetState.Deleted);
        _x.Requests.Should().ContainSingle();
        _x.Requests[0].Method.Should().Be(HttpMethod.Delete);
        _x.Requests[0].Uri.Should().Be(new Uri($"https://api.x.com/2/tweets/{tweetId}"));
    }
}
