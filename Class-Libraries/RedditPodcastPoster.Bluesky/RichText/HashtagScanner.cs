using System.Globalization;
using System.Text;
using RedditPodcastPoster.Text.HashTags;

namespace RedditPodcastPoster.Bluesky.RichText;

public readonly record struct HashtagToken(string Tag, long ByteStart, long ByteEnd);

/// <summary>
/// Finds hashtags in our own Bluesky posts. A tag starts with <c>#</c> or <c>＃</c> at the start
/// of text or after whitespace; opening quotes and brackets may sit between that boundary and the
/// <c>#</c>, so <c>"#OpusDei, …"</c> and <c>(#Cult)</c> link. A <c>#</c> inside a word or URL
/// (<c>a#b</c>, <c>/#x</c>) is not a hashtag.
/// <para>
/// The tag body starts with a letter, digit or <c>_</c>, then continues through letters, digits,
/// combining marks and <c>_</c>, and ends at the first other character — including BMP and
/// supplementary variation selectors (U+FE00–FE0F, U+E0100–E01EF). A combining mark or letter/other
/// number straight after <c>#</c> is not a tag. This deliberately differs from Bluesky's app rule,
/// which only ends a tag at whitespace and trims trailing punctuation: that rule links
/// <c>#Cult—and</c>, <c>#Cult's</c> and <c>#Cult🙏</c> whole. Our tags are inserted into titles as
/// whole words, so the facet must cover only the inserted tag. The characters a tag can hold live
/// in <see cref="HashtagText"/>, which <see cref="Find"/> uses so Bluesky and X share one
/// definition. <see cref="HashtagText.IsReproducedAsIs"/> is false when writing the tag drops or
/// changes a character, and the post factories then leave the stored tag out: <c>Q&amp;A</c> must
/// not be written as <c>QA</c>. A tag still needs one character that is not a digit or <c>_</c>,
/// and at most 64 characters.
/// </para>
/// </summary>
public static class HashtagScanner
{
    public const int MaxTagLength = HashtagText.MaxTagLength;

    public static IReadOnlyList<HashtagToken> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = new List<HashtagToken>();
        for (var i = 0; i < text.Length; i++)
        {
            if (!HashtagText.IsHashMark(text[i]) || !HasLeadingBoundary(text, i))
            {
                continue;
            }

            var bodyStart = i + 1;
            var bodyEnd = bodyStart;
            while (bodyEnd < text.Length && HashtagText.TryReadTagRune(text, bodyEnd, out var width))
            {
                bodyEnd += width;
            }

            // The body holds only letters, digits, marks and '_', so the only trailing character
            // to trim is '_' ("#Cult_" links "#Cult").
            var trimmedEnd = bodyEnd;
            while (trimmedEnd > bodyStart && text[trimmedEnd - 1] == '_')
            {
                trimmedEnd--;
            }

            var tag = text[bodyStart..trimmedEnd];
            if (HashtagText.IsValidTag(tag))
            {
                found.Add(new HashtagToken(tag, Utf8BytePosition(text, i), Utf8BytePosition(text, trimmedEnd)));
            }

            i = bodyEnd - 1;
        }

        return found;
    }

    /// <summary>
    /// The characters written after <c>#</c>. Same definition as <see cref="HashtagText.ToTagText"/>.
    /// </summary>
    public static string? ToTagText(string? hashTag) => HashtagText.ToTagText(hashTag);

    /// <summary>
    /// True when the stored tag can be written after <c>#</c> with the same characters.
    /// Same definition as <see cref="HashtagText.IsReproducedAsIs"/>.
    /// </summary>
    public static bool IsReproducedAsIs(string? stored) => HashtagText.IsReproducedAsIs(stored);

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

    private static long Utf8BytePosition(string text, int charIndex) =>
        Encoding.UTF8.GetByteCount(text.AsSpan(0, charIndex));
}
