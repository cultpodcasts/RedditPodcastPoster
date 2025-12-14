using X.Bluesky.Models;

namespace RedditPodcastPoster.Bluesky.Models;

public record BlueskyEmbedCardPost(string Text, Uri Url, IReadOnlyCollection<Image>? Images = null);