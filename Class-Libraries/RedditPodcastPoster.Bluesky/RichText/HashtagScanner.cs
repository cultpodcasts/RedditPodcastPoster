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
/// and <c>#Cult🙏</c> whole. Our tags are inserted into titles as whole words, so the facet must
/// cover only the inserted tag. The post factory writes every tag through <see cref="ToTagText"/>,
/// so a stored tag such as <c>Some-Tag</c> or <c>Jehovah’s Witnesses</c> is written in a form this
/// scanner links whole. A tag still needs one character that is not a digit or <c>_</c>, and at most 64
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

            // The body holds only letters, digits, marks and '_', so the only trailing character
            // to trim is '_' ("#Cult_" links "#Cult").
            var trimmedEnd = bodyEnd;
            while (trimmedEnd > bodyStart && text[trimmedEnd - 1] == '_')
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

    /// <summary>
    /// Returns the text to write after <c>#</c> so that <see cref="Find"/> links the whole tag:
    /// a leading <c>#</c> or <c>＃</c> is dropped, every character the tag body can't hold (dash,
    /// apostrophe, <c>&amp;</c>, <c>+</c>, space, emoji…) is removed, and trailing <c>_</c> is
    /// trimmed. Returns <c>null</c> when what is left can't be a hashtag (empty, digits and
    /// <c>_</c> only, or over <see cref="MaxTagLength"/>).
    /// </summary>
    public static string? ToTagText(string? hashTag)
    {
        if (string.IsNullOrEmpty(hashTag))
        {
            return null;
        }

        var builder = new StringBuilder(hashTag.Length);
        var index = 0;
        while (index < hashTag.Length && IsHashMark(hashTag[index]))
        {
            index++;
        }

        while (index < hashTag.Length)
        {
            if (TryReadTagRune(hashTag, index, out var width))
            {
                builder.Append(hashTag, index, width);
            }

            index += Math.Max(width, 1);
        }

        var tag = builder.ToString().TrimEnd('_');
        return IsValidTag(tag) ? tag : null;
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
        if (tag.Length == 0 ||
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
