using RedditPodcastPoster.Twitter.Dtos;

namespace RedditPodcastPoster.Twitter.Models;

public record GetTweetsResponseWrapper(GetTweetsState State, Tweet[]? Tweets = null);