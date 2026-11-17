using System.Net;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Description lines and sentences a show repeats across many of its catalogue episodes (sponsor reads,
/// support links, "consider supporting my work" blurbs, recurring organisation names). Built from the show's
/// catalogue page, then stripped from both sides before <see cref="CatalogueMatchScorer"/> compares
/// descriptions and proper nouns, so shared boilerplate is not evidence that two episodes are the same (#1445).
/// </summary>
public sealed partial class CatalogueDescriptionBoilerplate
{
    /// <summary>A segment is boilerplate once it appears in at least this many distinct catalogue descriptions.</summary>
    public const int MinSharedDescriptions = 3;

    private const int MinSegmentLength = 12;

    public static readonly CatalogueDescriptionBoilerplate None = new(new HashSet<string>());

    private readonly IReadOnlySet<string> _segments;

    private CatalogueDescriptionBoilerplate(IReadOnlySet<string> segments)
    {
        _segments = segments;
    }

    public bool IsEmpty => _segments.Count == 0;

    public static CatalogueDescriptionBoilerplate From(IEnumerable<string?> descriptions)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var description in descriptions)
        {
            foreach (var key in Segments(description).Select(s => s.Key).Distinct(StringComparer.Ordinal))
            {
                counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
            }
        }

        var shared = counts
            .Where(x => x.Value >= MinSharedDescriptions)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);
        return shared.Count == 0 ? None : new CatalogueDescriptionBoilerplate(shared);
    }

    /// <summary>Returns the description without its boilerplate segments (unchanged when nothing is shared).</summary>
    public string? Strip(string? description)
    {
        if (IsEmpty || string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        return string.Join("\n", Segments(description)
            .Where(s => !_segments.Contains(s.Key))
            .Select(s => s.Text));
    }

    private static IEnumerable<(string Key, string Text)> Segments(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            yield break;
        }

        var text = WebUtility.HtmlDecode(HtmlTag().Replace(description, "\n"));
        foreach (var raw in SegmentBreak().Split(text))
        {
            var segment = Whitespace().Replace(raw, " ").Trim();
            if (segment.Length < MinSegmentLength)
            {
                continue;
            }

            yield return (segment.ToLowerInvariant(), segment);
        }
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"(?<=[.!?])\s+|[\r\n]+")]
    private static partial Regex SegmentBreak();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
