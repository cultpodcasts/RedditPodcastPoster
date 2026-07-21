using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Bluesky.Models;

public record BlueskyEmbedCardPost(string Text, Uri Url, Service UrlService);