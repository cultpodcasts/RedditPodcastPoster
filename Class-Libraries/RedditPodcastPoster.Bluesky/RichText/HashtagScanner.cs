using System.Buffers;
using System.Globalization;
using System.Text;

namespace RedditPodcastPoster.Bluesky.RichText;

public readonly record struct HashtagToken(string Tag, long ByteStart, long ByteEnd);

/// <summary>
/// Finds hashtags in our own Bluesky posts. A tag starts with <c>#</c> or <c>＃</c> at the start
/// of text or after whitespace; opening quotes and brackets may sit between that boundary and the
/// <c>#</c>, so <c>"#OpusDei, …"</c> and <c>(#Cult)</c> link. A <c>#</c> inside a word or URL
/// (<c>a#b</c>, <c>/#x</c>) is not a hashtag.
/// <para>
/// The tag body is a run of letters, digits, combining marks and <c>_</c>, and it ends at the first
/// other character. This deliberately differs from Bluesky's app rule, which only ends a tag at
/// whitespace and trims trailing punctuation: that rule links <c>#Cult—and</c>, <c>#Cult's</c>
/// and <c>#Cult🙏</c> whole. Our tags are inserted into titles as whole words (and enrichment
/// tags have spaces, dots, apostrophes and hyphens removed), so the facet must cover only the
/// inserted tag. A tag still needs one character that is not a digit or <c>_</c>, and at most 64
/// characters.
/// </para>
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
            while (bodyEnd < text.Length && TryReadTagRune(text, bodyEnd, out var width))
            {
                bodyEnd += width;
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

    // A tag continues through letters, digits, '_' and combining marks (accents, Indic vowel signs),
    // read as whole runes so letters outside the BMP count. Anything else ends it: dashes,
    // apostrophes, quotes, punctuation, symbols and emoji, whitespace, and invisible breaks such as
    // the zero-width joiner. Variation selectors (U+FE00–U+FE0F) also end it, as Bluesky's
    // rule rejects U+FE0F.
    private static bool TryReadTagRune(string text, int index, out int width)
    {
        if (Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out width) != OperationStatus.Done)
        {
            return false;
        }

        if (rune.Value == '_')
        {
            return true;
        }

        // U+02BC (modifier letter apostrophe) is a letter by category but reads as an apostrophe.
        if (rune.Value is >= 0xFE00 and <= 0xFE0F or 0x02BC)
        {
            return false;
        }

        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
    }

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
