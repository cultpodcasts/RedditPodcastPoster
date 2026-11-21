using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Bluesky.Models;

/// <summary>
/// Bluesky post text and link. <see cref="PlatformUrl"/> is set when <see cref="Url"/> is the
/// shortener, so the card is still built from the YouTube or Spotify API. <see cref="WithoutCardImage"/> is set
/// when a curator removed the episode's image (#1500): the card keeps its title and description but has no
/// thumbnail, so the removed (YouTube / Spotify) art is never posted.
/// </summary>
public record BlueskyEmbedCardPost(
    string Text,
    Uri Url,
    Service UrlService,
    Uri? PlatformUrl = null,
    bool WithoutCardImage = false);