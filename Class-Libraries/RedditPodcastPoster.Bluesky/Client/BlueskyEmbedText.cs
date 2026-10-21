namespace RedditPodcastPoster.Bluesky.Client;

/// <summary>
/// <c>idunno.Bluesky.Embed.External.Properties</c> rejects a null title or description and a blank uri.
/// The lexicon does not set grapheme caps on those strings, so a YouTube description is capped here
/// before it is placed on the embed.
/// </summary>
public static class BlueskyEmbedText
{
    public const int MaxTitleLength = 300;
    public const int MaxDescriptionLength = 3000;

    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        var cut = maxLength - 1;
        if (cut > 0 && char.IsLowSurrogate(trimmed[cut]))
        {
            cut--;
        }

        return trimmed[..cut] + "…";
    }
}
