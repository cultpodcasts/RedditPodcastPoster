using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Expands the recognised tokens of a podcast's <see cref="Podcast.DescriptionRegex"/> and builds the
/// <see cref="Regex"/>. This is the single place a description regex should be compiled.
/// <list type="bullet">
/// <item><c>{episode-title}</c> - the episode's (raw, un-sanitised) title.</item>
/// <item><c>{podcast-name}</c> - the podcast's name.</item>
/// </list>
/// <para>
/// Each token is replaced by a non-capturing group that matches the (HTML-decoded) value literally, so it is
/// never interpreted as regex syntax (every character is <see cref="Regex.Escape"/>d). The group tolerates:
/// any run of whitespace where the value has whitespace (<c>\s+</c>), leading/trailing whitespace in the value
/// (trimmed), and apostrophe, quote, dash and ampersand variants (typographic forms and HTML entities).
/// Because the replacement is a group, a quantifier written after a token (<c>{episode-title}?</c>) applies to
/// the whole value.
/// </para>
/// <para>
/// A token with no value (null, empty or whitespace) expands to <c>(?!)</c>, which never matches, so the
/// pattern simply fails to match and the description is left unchanged - it never throws and never turns into
/// an empty, match-anything group.
/// </para>
/// <para>
/// Only the exact, lower-case tokens are recognised. Any other <c>{...}</c> text - including regex quantifiers
/// such as <c>{2}</c> or <c>{2,3}</c> - is left exactly as written. A token whose opening brace is escaped
/// (<c>\{episode-title}</c>) is literal text, not a token. A pattern without tokens is compiled exactly as written.
/// </para>
/// </summary>
public static partial class DescriptionRegexTokens
{
    public const string EpisodeTitle = "{episode-title}";
    public const string PodcastName = "{podcast-name}";

    public static readonly IReadOnlyList<string> Tokens = [EpisodeTitle, PodcastName];

    internal const string NeverMatches = "(?!)";

    /// <summary>
    /// Sample values used to validate a pattern containing tokens. They deliberately contain regex
    /// metacharacters and typographic characters.
    /// </summary>
    internal const string SampleEpisodeTitle = "Sample: Episode (Part 1) \u2014 What's Next? [Live] & More";

    internal const string SamplePodcastName = "Sample-Podcast & Co.";

    /// <summary>
    /// Characters (and the HTML entities that can stand in for them in an un-decoded description) that are treated
    /// as interchangeable when matching a token value.
    /// </summary>
    private static readonly (string Chars, string[] Entities)[] EquivalenceClasses =
    [
        ("\u0027\u2019\u2018\u00B4`", ["&#39;", "&#x27;", "&apos;", "&rsquo;", "&lsquo;", "&#8217;", "&#8216;", "&#x2019;", "&#x2018;"]),
        ("\"\u201C\u201D", ["&quot;", "&#34;", "&#x22;", "&ldquo;", "&rdquo;", "&#8220;", "&#8221;", "&#x201C;", "&#x201D;"]),
        ("-\u2013\u2014", ["&ndash;", "&mdash;", "&#8211;", "&#8212;", "&#x2013;", "&#x2014;", "&#45;"]),
        ("&", ["&amp;", "&#38;", "&#x26;"])
    ];

    // A token preceded by an odd number of backslashes (e.g. \{episode-title}) is an escaped literal brace, not a
    // token; an even number (\\{episode-title}) is literal backslash(es) followed by a token.
    [GeneratedRegex(@"(?<=(?<!\\)(?:\\\\)*)\{(?:episode-title|podcast-name)\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    public static bool ContainsTokens(string? pattern)
    {
        return !string.IsNullOrEmpty(pattern) && TokenRegex().IsMatch(pattern);
    }

    /// <summary>
    /// Replaces every recognised token in <paramref name="pattern" /> in a single pass (a value that itself
    /// contains token text is not re-expanded).
    /// </summary>
    public static string Expand(string pattern, string? episodeTitle, string? podcastName)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return pattern;
        }

        return TokenRegex().Replace(pattern, match => match.Value switch
        {
            EpisodeTitle => ToPattern(episodeTitle),
            PodcastName => ToPattern(podcastName),
            _ => match.Value
        });
    }

    /// <summary>
    /// Expands the tokens and builds the regex with <see cref="Podcast.DescriptionFlags" />.
    /// Returns null when there is no pattern.
    /// </summary>
    public static Regex? Create(string? pattern, string? episodeTitle, string? podcastName)
    {
        return Create(pattern, episodeTitle, podcastName, Podcast.DescriptionFlags);
    }

    /// <summary>
    /// As <see cref="Create(string?, string?, string?)" />, but a pattern WITHOUT tokens is compiled with
    /// <paramref name="tokenFreeOptions" />. Only for callers that historically compiled the description regex
    /// with other options and must keep behaving exactly as before for existing (token-free) patterns. A pattern
    /// with tokens always uses <see cref="Podcast.DescriptionFlags" />.
    /// </summary>
    public static Regex? Create(
        string? pattern,
        string? episodeTitle,
        string? podcastName,
        RegexOptions tokenFreeOptions)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        if (!ContainsTokens(pattern))
        {
            return new Regex(pattern, tokenFreeOptions);
        }

        return new Regex(Expand(pattern, episodeTitle, podcastName), Podcast.DescriptionFlags);
    }

    /// <summary>
    /// Validates a description regex. Tokens are expanded with sample values before compiling, so a pattern
    /// containing tokens is accepted when the rest of it is a valid regex. Null/blank patterns are valid (they
    /// clear the regex).
    /// </summary>
    public static bool TryValidate(string? pattern, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        try
        {
            _ = Create(pattern, SampleEpisodeTitle, SamplePodcastName);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static string ToPattern(string? value)
    {
        var decoded = WebUtility.HtmlDecode(value ?? string.Empty);
        decoded = Whitespace().Replace(decoded, " ").Trim();
        if (decoded.Length == 0)
        {
            return NeverMatches;
        }

        var builder = new StringBuilder("(?:");
        foreach (var character in decoded)
        {
            if (character == ' ')
            {
                builder.Append(@"\s+");
                continue;
            }

            var equivalent = Array.Find(EquivalenceClasses, x => x.Chars.Contains(character));
            if (equivalent.Chars == null)
            {
                builder.Append(Regex.Escape(character.ToString()));
                continue;
            }

            builder.Append("(?:[");
            foreach (var member in equivalent.Chars)
            {
                builder.Append(Regex.Escape(member.ToString()).Replace("-", @"\-"));
            }

            builder.Append(']');
            foreach (var entity in equivalent.Entities)
            {
                builder.Append('|').Append(Regex.Escape(entity));
            }

            builder.Append(')');
        }

        builder.Append(')');
        return builder.ToString();
    }
}