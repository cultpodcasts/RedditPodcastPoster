namespace RedditPodcastPoster.Bluesky.Client;

public sealed record BlueskyPlatformCard(Uri Link, string Title, string Description, Uri ImageUrl);

public sealed record BlueskyCardImage(byte[] Bytes, string MimeType);
