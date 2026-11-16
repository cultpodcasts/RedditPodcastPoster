using System.Text.RegularExpressions;

namespace RedditPodcastPoster.UrlSubmission.Matching;

/// <summary>
/// Part and episode numbers carried in an episode title, so multi-part releases with an identical
/// stem (for example <c>(pt.I)</c> vs <c>(pt.II)</c>) are not treated as the same episode (#1445 review).
/// Markers only conflict within the same kind (part, episode, leading <c>#N</c>, trailing <c>(N)</c>),
/// and parsing is conservative so ordinary titles (years, rankings, <c>Tai Chi</c>, <c>EPIX</c>) carry no marker.
/// </summary>
public static partial class EpisodeTitlePartMarkers
{
    private const int MaxTrailingNumber = 99;
    private const int FirstYear = 1900;
    private const int LastYear = 2099;

    private static readonly IReadOnlyDictionary<string, int> NumberWords =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
            ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10
        };

    // A Roman numeral or number word must be separated from its keyword by a dot or whitespace, so
    // "Tai Chi" (ch + i), "PTV" (pt + v) and "EPIX" (ep + ix) do not parse. Digits may follow directly ("pt2").
    private const string WordOrRoman = @"(?:\.\s*|\s+)(?<n>[ivx]+|one|two|three|four|five|six|seven|eight|nine|ten)\b";
    private const string Digits = @"\.?\s*(?<n>\d{1,4})\b";

    /// <summary>Part markers: pt, part, chapter or ch.</summary>
    [GeneratedRegex(
        @"\b(?:pt|part|chapter|ch)(?:" + WordOrRoman + "|" + Digits + ")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartPattern();

    /// <summary>Episode markers: ep or episode, optionally followed by <c>#</c>.</summary>
    [GeneratedRegex(
        @"\b(?:ep|episode)(?:" + WordOrRoman + @"|\.?\s*#?\s*(?<n>\d{1,4})\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodePattern();

    /// <summary>
    /// <c>#N</c> only at the start of the title, right after a separator, or right before one
    /// (<c>#12: …</c>, <c>… | #12</c>, <c>#12 - …</c>). A mid-title <c>#1</c> is a ranking, not an episode.
    /// </summary>
    [GeneratedRegex(
        @"(?:^\s*|[:|\-–—]\s*)#(?<n>\d{1,4})\b|#(?<n>\d{1,4})\s*(?=[:|\-–—])",
        RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();

    /// <summary>A trailing bracketed number, <c>(2)</c> or <c>(II)</c>. Capped so <c>(2024)</c> is not read as a part.</summary>
    [GeneratedRegex(
        @"\(\s*(?<n>\d{1,2}|[ivx]+)\s*\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingPattern();

    [GeneratedRegex(@"^(?:x{0,3})(?:ix|iv|v?i{0,3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RomanPattern();

    /// <summary>
    /// <see langword="true"/> when both titles carry the same kind of marker and the numbers differ.
    /// Titles where only one side has a given kind of marker do not conflict on that kind.
    /// </summary>
    public static bool Conflict(string left, string right) =>
        Differs(Parts(left), Parts(right)) ||
        Differs(Episodes(left), Episodes(right)) ||
        Differs(Hashes(left), Hashes(right)) ||
        Differs(Trailing(left), Trailing(right));

    public static IReadOnlySet<int> Parts(string title) => Extract(PartPattern(), title);

    public static IReadOnlySet<int> Episodes(string title) => Extract(EpisodePattern(), title);

    /// <summary>Leading or separator-bound <c>#N</c>; ignored when the title also has an ep/episode number.</summary>
    public static IReadOnlySet<int> Hashes(string title) =>
        Episodes(title).Count > 0 ? new HashSet<int>() : Extract(HashPattern(), title);

    public static IReadOnlySet<int> Trailing(string title)
    {
        var numbers = Extract(TrailingPattern(), title);
        return numbers.Where(n => n <= MaxTrailingNumber).ToHashSet();
    }

    private static bool Differs(IReadOnlySet<int> left, IReadOnlySet<int> right) =>
        left.Count > 0 && right.Count > 0 && !left.SetEquals(right);

    private static HashSet<int> Extract(Regex pattern, string title)
    {
        var numbers = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(title))
        {
            return numbers;
        }

        foreach (Match match in pattern.Matches(title))
        {
            if (TryParseNumber(match.Groups["n"].Value, out var number) && !IsYear(number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    private static bool IsYear(int number) => number is >= FirstYear and <= LastYear;

    private static bool TryParseNumber(string token, out int number)
    {
        if (int.TryParse(token, out number))
        {
            return true;
        }

        if (NumberWords.TryGetValue(token, out number))
        {
            return true;
        }

        if (token.Length > 0 && RomanPattern().IsMatch(token))
        {
            number = RomanToInt(token);
            return number > 0;
        }

        number = 0;
        return false;
    }

    private static int RomanToInt(string roman)
    {
        var total = 0;
        var previous = 0;
        for (var i = roman.Length - 1; i >= 0; i--)
        {
            var value = char.ToLowerInvariant(roman[i]) switch
            {
                'i' => 1,
                'v' => 5,
                'x' => 10,
                _ => 0
            };
            total += value < previous ? -value : value;
            previous = Math.Max(previous, value);
        }

        return total;
    }
}
