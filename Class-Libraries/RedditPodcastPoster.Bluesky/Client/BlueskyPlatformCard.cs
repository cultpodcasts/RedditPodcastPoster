namespace RedditPodcastPoster.Bluesky.Client;

public sealed record BlueskyPlatformCard(
    Uri Link,
    string Title,
    string Description,
    IReadOnlyList<Uri> ImageUrls);

/// <summary>An API-built link card. <see cref="Image"/> is null for a card posted without a thumbnail (#1500).</summary>
public sealed record BlueskyApiCard(Uri Link, string Title, string Description, BlueskyCardImage? Image);

public sealed record BlueskyCardImage(byte[] Bytes, string MimeType);
