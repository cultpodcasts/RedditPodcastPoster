using System.Text.RegularExpressions;

namespace RedditPodcastPoster.UrlSubmission.Matching;

/// <summary>
/// Part and episode numbers carried in an episode title, so multi-part releases with an identical
/// stem (for example <c>(pt.I)</c> vs <c>(pt.II)</c>) are not treated as the same episode (#1445 review).
/// </summary>
public static partial class EpisodeTitlePartMarkers
{
    private static readonly IReadOnlyDictionary<string, int> NumberWords =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
            ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10
        };

    /// <summary>Part, chapter or trailing <c>(N)</c> markers.</summary>
    [GeneratedRegex(
        @"\b(?:pt|part|chapter|ch)\.?\s*(?<n>\d+|[ivx]+|one|two|three|four|five|six|seven|eight|nine|ten)\b|\(\s*(?<n>\d+|[ivx]+)\s*\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartPattern();

    /// <summary>Episode markers: <c>ep</c>, <c>episode</c> or <c>#</c> followed by a number.</summary>
    [GeneratedRegex(
        @"\b(?:ep|episode)\.?\s*#?\s*(?<n>\d+|[ivx]+|one|two|three|four|five|six|seven|eight|nine|ten)\b|#(?<n>\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodePattern();

    [GeneratedRegex(@"^(?:x{0,3})(?:ix|iv|v?i{0,3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RomanPattern();

    /// <summary>
    /// <see langword="true"/> when both titles carry a part marker, or both carry an episode marker,
    /// and the numbers differ. Titles where only one side has a marker do not conflict.
    /// </summary>
    public static bool Conflict(string left, string right) =>
        Differs(Parts(left), Parts(right)) || Differs(Episodes(left), Episodes(right));

    public static IReadOnlySet<int> Parts(string title) => Extract(PartPattern(), title);

    public static IReadOnlySet<int> Episodes(string title) => Extract(EpisodePattern(), title);

    private static bool Differs(IReadOnlySet<int> left, IReadOnlySet<int> right) =>
        left.Count > 0 && right.Count > 0 && !left.SetEquals(right);

    private static IReadOnlySet<int> Extract(Regex pattern, string title)
    {
        var numbers = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(title))
        {
            return numbers;
        }

        foreach (Match match in pattern.Matches(title))
        {
            if (TryParseNumber(match.Groups["n"].Value, out var number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

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
