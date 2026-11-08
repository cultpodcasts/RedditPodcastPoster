using System.Buffers;
using System.Globalization;
using System.Text;

namespace RedditPodcastPoster.Text.HashTags;

/// <summary>
/// Characters a hashtag body can hold on both Bluesky and X (letters, digits, combining marks,
/// and <c>_</c>). That intersection is the only permitted stored HashTag body:
/// <c>&amp;</c>, <c>+</c>, <c>-</c>, apostrophe, space, emoji and other punctuation are not
/// permitted. <see cref="ToTagText"/> is the text written after <c>#</c>.
/// <see cref="IsPermittedHashTag"/> / <see cref="IsReproducedAsIs"/> are false when writing
/// would drop or change a character.
/// </summary>
public static class HashtagText
{
    public const int MaxTagLength = 64;

    /// <summary>
    /// Returns the text to write after <c>#</c> so a facet can link the whole tag: a leading
    /// <c>#</c> or <c>＃</c> is dropped, every character the tag body can't hold (dash, apostrophe,
    /// <c>&amp;</c>, <c>+</c>, space, emoji, BMP and supplementary variation selectors…) is removed,
    /// and trailing <c>_</c> is trimmed. The first remaining body rune must be a letter, digit or
    /// <c>_</c> — a combining mark or letter/other number straight after <c>#</c> yields
    /// <c>null</c>. Also returns <c>null</c> when what is left can't be a hashtag (empty, digits
    /// and <c>_</c> only, or over <see cref="MaxTagLength"/>). Prefer
    /// <see cref="IsPermittedHashTag"/> at accept paths so tags that need stripping are never stored.
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
    /// True when the value is a permitted HashTag on both X and Bluesky: after an optional leading
    /// <c>#</c>/<c>＃</c>, every character is a letter, digit, combining mark, or <c>_</c>, and the
    /// body is a valid tag (not empty, not digits/<c>_</c> only, ≤ <see cref="MaxTagLength"/>
    /// graphemes). <c>Q&amp;A</c>, <c>C++</c>, and <c>Some-Tag</c> are not permitted.
    /// </summary>
    /// <summary>Accept filter: only X∩Bluesky-reproducible tags become HashTags (#1304 reject).</summary>
    public static bool IsPermittedHashTag(string? stored) => IsReproducedAsIs(stored);

    /// <summary>
    /// True when the stored tag can be written after <c>#</c> with the same characters.
    /// Same rule as <see cref="IsPermittedHashTag"/>.
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

    /// <summary>
    /// Compacted hashtag body for an enrichment phrase: optional leading hash marks and all
    /// whitespace are removed. Returns <c>null</c> when the compacted body is not a permitted
    /// platform hashtag (e.g. <c>Some-Tag</c>, <c>Q&amp;A</c>). Multi-word phrases such as
    /// <c>Alpha Beta</c> compact to <c>AlphaBeta</c>.
    /// </summary>
    public static string? ToEnrichmentHashtagBody(string? enrichmentTag)
    {
        if (string.IsNullOrWhiteSpace(enrichmentTag))
        {
            return null;
        }

        var body = enrichmentTag.Trim();
        var index = 0;
        while (index < body.Length && IsHashMark(body[index]))
        {
            index++;
        }

        body = body[index..];
        if (body.Length == 0)
        {
            return null;
        }

        var compacted = string.Concat(body.Where(static c => !char.IsWhiteSpace(c)));
        return IsPermittedHashTag(compacted) ? compacted : null;
    }

    /// <summary>
    /// Keeps only space-delimited tokens that are <see cref="IsPermittedHashTag"/>; returns
    /// <c>null</c> when none remain (or the input is blank).
    /// </summary>
    public static string? FilterPermittedStoredHashTagField(string? hashTag)
    {
        if (string.IsNullOrWhiteSpace(hashTag))
        {
            return null;
        }

        var permitted = hashTag
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsPermittedHashTag)
            .ToArray();
        return permitted.Length == 0 ? null : string.Join(' ', permitted);
    }

    /// <summary>
    /// Keeps enrichment phrases whose compacted body is a permitted platform hashtag; returns
    /// <c>null</c> when none remain.
    /// </summary>
    public static string[]? FilterPermittedEnrichmentHashTags(IEnumerable<string>? enrichmentHashTags)
    {
        if (enrichmentHashTags is null)
        {
            return null;
        }

        var permitted = enrichmentHashTags
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .Where(static x => ToEnrichmentHashtagBody(x) is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return permitted.Length == 0 ? null : permitted;
    }

    /// <summary>
    /// Text written after <c>#</c> when enriching a title with this hashtag, or <c>null</c> when
    /// enrichment must be skipped.
    /// Enrichment multi-word tags keep the phrase on <paramref name="tag"/> and the compacted
    /// permitted hashtag on <paramref name="matchingText"/> (e.g. <c>Alpha Beta</c> /
    /// <c>#AlphaBeta</c>). Gate those on <paramref name="matchingText"/> so spaces on the phrase
    /// do not block enrichment. Tags without whitespace gate on the stored <paramref name="tag"/>,
    /// which must already be a permitted platform hashtag.
    /// </summary>
    public static string? GetTitleEnrichmentTagText(string tag, string? matchingText)
    {
        var writtenForm = matchingText != null && tag.Any(char.IsWhiteSpace)
            ? matchingText
            : tag;
        return IsPermittedHashTag(writtenForm) ? ToTagText(writtenForm) : null;
    }

    internal static bool IsHashMark(char c) => c is '#' or '＃';

    // A tag continues through letters, digits, '_' and combining marks (accents, Indic vowel signs),
    // read as whole runes so letters outside the BMP count. Anything else ends it: dashes,
    // apostrophes, quotes, punctuation, symbols and emoji, whitespace, and invisible breaks such as
    // ZWNJ/ZWJ. Variation selectors (U+FE00–U+FE0F and supplementary U+E0100–U+E01EF) also end it.
    // First-body rejection (letter/digit/_ only) is in IsValidTag / IsTagBodyStartRune, not here —
    // Mn/Mc are valid continuations after a legal start.
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

    // First body rune must be a letter, digit or '_' (#1084) — combining marks / Nl/No after '#'
    // are not a tag. Continuation Mn/Mc after a legal start are accepted by TryReadTagRune.
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
