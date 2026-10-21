namespace RedditPodcastPoster.Bluesky.Client;

public sealed record BlueskyPlatformCard(
    Uri Link,
    string Title,
    string Description,
    IReadOnlyList<Uri> ImageUrls);

public sealed record BlueskyApiCard(Uri Link, string Title, string Description, BlueskyCardImage Image);

public sealed record BlueskyCardImage(byte[] Bytes, string MimeType);
