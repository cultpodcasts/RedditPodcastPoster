using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky;

public record BlueskyEmbedCardPost(string Text, Uri Url, Service UrlService);