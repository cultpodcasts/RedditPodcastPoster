using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Bluesky.Models;

/// <summary>
/// Bluesky post text and link. <see cref="PlatformUrl"/> is set when <see cref="Url"/> is the
/// shortener, so the card is still built from the YouTube or Spotify API.
/// </summary>
public record BlueskyEmbedCardPost(string Text, Uri Url, Service UrlService, Uri? PlatformUrl = null);