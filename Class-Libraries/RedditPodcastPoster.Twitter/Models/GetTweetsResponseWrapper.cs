using RedditPodcastPoster.Twitter.Dtos;

namespace RedditPodcastPoster.Twitter.Models;

/// <param name="NextToken">X's <c>meta.next_token</c>: set when there is another page of the timeline.</param>
public record GetTweetsResponseWrapper(GetTweetsState State, Tweet[]? Tweets = null, string? NextToken = null);
