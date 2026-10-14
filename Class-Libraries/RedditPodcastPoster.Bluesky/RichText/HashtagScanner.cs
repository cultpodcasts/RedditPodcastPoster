using System.Globalization;
using System.Text;

namespace RedditPodcastPoster.Bluesky.RichText;

public readonly record struct HashtagToken(string Tag, long ByteStart, long ByteEnd);

/// <summary>
/// Finds Bluesky hashtags using the official app rule: a <c>#</c> or <c>＃</c> at the start of
/// text or after whitespace, trailing punctuation trimmed, at least one character that is not a
/// digit or punctuation, and at most 64 characters. Opening quotes and brackets may also sit
/// between that boundary and the <c>#</c>, so <c>"#OpusDei, …"</c> and <c>(#Cult)</c> link.
/// A <c>#</c> inside a word or URL (<c>a#b</c>, <c>/#x</c>) is not a hashtag.
/// </summary>
public static class HashtagScanner
{
    public const int MaxTagLength = 64;

    public static IReadOnlyList<HashtagToken> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = new List<HashtagToken>();
        for (var i = 0; i < text.Length; i++)
        {
            if (!IsHashMark(text[i]) || !HasLeadingBoundary(text, i))
            {
                continue;
            }

            var bodyStart = i + 1;
            var bodyEnd = bodyStart;
            while (bodyEnd < text.Length && !IsTagTerminator(text[bodyEnd]))
            {
                bodyEnd++;
            }

            var trimmedEnd = bodyEnd;
            while (trimmedEnd > bodyStart && char.IsPunctuation(text[trimmedEnd - 1]))
            {
                trimmedEnd--;
            }

            var tag = text[bodyStart..trimmedEnd];
            if (IsValidTag(tag))
            {
                found.Add(new HashtagToken(tag, Utf8BytePosition(text, i), Utf8BytePosition(text, trimmedEnd)));
            }

            i = bodyEnd - 1;
        }

        return found;
    }

    private static bool IsHashMark(char c) => c is '#' or '＃';

    // Start of text or whitespace, then any run of opening quotes or brackets.
    private static bool HasLeadingBoundary(string text, int hashIndex)
    {
        var j = hashIndex - 1;
        while (j >= 0 && IsOpener(text[j]))
        {
            j--;
        }

        return j < 0 || char.IsWhiteSpace(text[j]);
    }

    private static bool IsOpener(char c) =>
        c is '"' or '\'' ||
        CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.OpenPunctuation
            or UnicodeCategory.InitialQuotePunctuation;

    // Whitespace, plus the invisible characters Bluesky's tag rule treats as breaks
    // (soft hyphen, word joiner, hair space, zero-width space/non-joiner/joiner, keycap).
    private static bool IsTagTerminator(char c) =>
        char.IsWhiteSpace(c) || c is '\u00AD' or '\u2060' or '\u200A' or '\u200B' or '\u200C' or '\u200D' or '\u20E2';

    private static bool IsValidTag(string tag)
    {
        if (tag.Length == 0 || tag[0] == '\uFE0F' ||
            new StringInfo(tag).LengthInTextElements > MaxTagLength)
        {
            return false;
        }

        foreach (var c in tag)
        {
            if (!char.IsDigit(c) && !char.IsPunctuation(c))
            {
                return true;
            }
        }

        return false;
    }

    private static long Utf8BytePosition(string text, int charIndex) =>
        Encoding.UTF8.GetByteCount(text.AsSpan(0, charIndex));
}
