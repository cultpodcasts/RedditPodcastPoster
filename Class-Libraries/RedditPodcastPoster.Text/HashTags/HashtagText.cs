using System.Buffers;
using System.Globalization;
using System.Text;

namespace RedditPodcastPoster.Text.HashTags;

/// <summary>
/// Characters a hashtag body can hold on both Bluesky and X. <see cref="ToTagText"/> is the text
/// written after <c>#</c>. <see cref="IsReproducedAsIs"/> is false when that drops or changes a
/// character, so a stored tag such as <c>Q&amp;A</c> or <c>Some-Tag</c> is not written as
/// <c>QA</c> or <c>SomeTag</c>.
/// </summary>
public static class HashtagText
{
    public const int MaxTagLength = 64;

    /// <summary>
    /// Returns the text to write after <c>#</c> so a facet can link the whole tag: a leading
    /// <c>#</c> or <c>＃</c> is dropped, every character the tag body can't hold (dash, apostrophe,
    /// <c>&amp;</c>, <c>+</c>, space, emoji…) is removed, and trailing <c>_</c> is trimmed.
    /// Returns <c>null</c> when what is left can't be a hashtag (empty, digits and <c>_</c> only,
    /// or over <see cref="MaxTagLength"/>).
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

    /// <summary>
    /// True when the stored tag can be written after <c>#</c> with the same characters.
    /// <c>Q&amp;A</c> is not: the characters a tag can hold leave <c>QA</c>, which is a different tag.
    /// </summary>
    public static bool IsReproducedAsIs(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var body = stored.Trim();
        var index = 0;
        while (index < body.Length && IsHashMark(body[index]))
        {
            index++;
        }

        body = body[index..];
        if (body.Length == 0)
        {
            return false;
        }

        var written = ToTagText(body);
        return written != null && string.Equals(written, body, StringComparison.Ordinal);
    }

    internal static bool IsHashMark(char c) => c is '#' or '＃';

    // A tag continues through letters, digits, '_' and combining marks (accents, Indic vowel signs),
    // read as whole runes so letters outside the BMP count. Anything else ends it: dashes,
    // apostrophes, quotes, punctuation, symbols and emoji, whitespace, and invisible breaks such as
    // ZWNJ/ZWJ. Variation selectors (U+FE00–U+FE0F and supplementary U+E0100–U+E01EF) also end it.
    // The first body character must be a letter, digit or '_' — a combining mark straight after '#'
    // is not a tag (#1084).
    internal static bool TryReadTagRune(string text, int index, out int width)
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
        // Variation selectors are Mn but must not stay inside a tag (BMP FE00–FE0F and supplementary).
        if (rune.Value is (>= 0xFE00 and <= 0xFE0F) or (>= 0xE0100 and <= 0xE01EF) or 0x02BC)
        {
            return false;
        }

        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
    }

    internal static bool IsValidTag(string tag)
    {
        if (tag.Length == 0 ||
            new StringInfo(tag).LengthInTextElements > MaxTagLength)
        {
            return false;
        }

        if (Rune.DecodeFromUtf16(tag.AsSpan(), out var first, out _) != OperationStatus.Done ||
            !IsTagBodyStartRune(first))
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

    private static bool IsTagBodyStartRune(Rune rune)
    {
        if (rune.Value == '_')
        {
            return true;
        }

        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber;
    }
}
