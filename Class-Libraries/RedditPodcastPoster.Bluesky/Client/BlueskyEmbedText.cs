using System.Globalization;

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

        return TruncateToWholeTextElements(trimmed, maxLength - 1) + "…";
    }

    /// <summary>
    /// Longest prefix of <paramref name="text"/> made of whole text elements (graphemes) whose UTF-16 length is at
    /// most <paramref name="maxLength"/>, so a cut never splits a surrogate pair, emoji sequence or combining mark.
    /// </summary>
    public static string TruncateToWholeTextElements(string text, int maxLength)
    {
        var length = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var next = enumerator.ElementIndex + enumerator.GetTextElement().Length;
            if (next > maxLength)
            {
                break;
            }

            length = next;
        }

        return text[..length];
    }
}
