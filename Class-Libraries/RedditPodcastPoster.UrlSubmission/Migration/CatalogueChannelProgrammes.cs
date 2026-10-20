using System.Globalization;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Migration;

/// <summary>
/// Programmes on a channel or strand. The publisher name is not a programme.
/// An iPlayer <c>series</c>/<c>season</c> slug, a repeated title, or a brand shared by
/// two or more episodes is one programme. A single remaining title is its own programme.
/// </summary>
public static class CatalogueChannelProgrammes
{
    public sealed record Programme(string Query, IReadOnlyList<Guid> EpisodeIds);

    private static readonly Regex PartMarker = new(
        @"\s*\(part\s+\d+\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutletSuffix = new(
        @"\s+-\s+BBC(?:\s+(?:One|Two|Three|Four|News))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SeriesInTitle = new(
        @"^(?<show>.+?)\s+-\s+Series\s+\d+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SeriesInSlug = new(
        @"^(?<show>.+?)-(?:series|season)-\d+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BrandBeforeHyphen = new(
        @"^(?<brand>.+?)\s+-\s+\S",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<Programme> FromEpisodes(IEnumerable<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        var rows = new List<Row>();
        foreach (var episode in episodes)
        {
            if (episode.Removed || string.IsNullOrWhiteSpace(episode.Title))
            {
                continue;
            }

            var cleaned = Clean(episode.Title);
            if (cleaned.Length == 0)
            {
                continue;
            }

            rows.Add(new Row(episode.Id, cleaned, SeriesName(episode, cleaned)));
        }

        var used = new HashSet<Guid>();
        var programmes = new List<Programme>();

        foreach (var group in rows.Where(row => row.Series is not null)
                     .GroupBy(row => row.Series!, StringComparer.OrdinalIgnoreCase))
        {
            programmes.Add(Take(group.First().Series!, group, used));
        }

        var rest = rows.Where(row => !used.Contains(row.Id)).ToList();
        foreach (var group in rest.GroupBy(row => row.Cleaned, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            programmes.Add(Take(group.First().Cleaned, group, used));
        }

        rest = rest.Where(row => !used.Contains(row.Id)).ToList();
        var branded = new Dictionary<string, List<Row>>(StringComparer.OrdinalIgnoreCase);
        var unbranded = new List<Row>();
        foreach (var row in rest)
        {
            var brand = SharedBrand(row.Cleaned);
            if (brand is null)
            {
                unbranded.Add(row);
                continue;
            }

            if (!branded.TryGetValue(brand, out var list))
            {
                list = [];
                branded[brand] = list;
            }

            list.Add(row);
        }

        foreach (var (brand, list) in branded)
        {
            if (list.Count < 2)
            {
                unbranded.Add(list[0]);
                continue;
            }

            programmes.Add(new Programme(brand, list.Select(row => row.Id).ToList()));
        }

        foreach (var row in unbranded)
        {
            programmes.Add(new Programme(row.Cleaned, [row.Id]));
        }

        return programmes;
    }

    private static Programme Take(string query, IEnumerable<Row> rows, HashSet<Guid> used)
    {
        var ids = rows.Select(row => row.Id).ToList();
        foreach (var id in ids)
        {
            used.Add(id);
        }

        return new Programme(query, ids);
    }

    private static string Clean(string title)
    {
        var value = Regex.Replace(title.Trim(), @"\s+", " ");
        value = Regex.Replace(value, @"\s+-\s+", " - ");
        string previous;
        do
        {
            previous = value;
            value = OutletSuffix.Replace(value, string.Empty).Trim();
        } while (!value.Equals(previous, StringComparison.Ordinal) && value.Length > 0);

        return PartMarker.Replace(value, string.Empty).Trim();
    }

    private static string? SeriesName(Episode episode, string cleaned)
    {
        var fromTitle = SeriesInTitle.Match(cleaned);
        if (fromTitle.Success)
        {
            var show = fromTitle.Groups["show"].Value.Trim();
            if (show.Length > 0)
            {
                return show;
            }
        }

        var slug = IplayerSlug(episode);
        if (slug is null)
        {
            return null;
        }

        var fromSlug = SeriesInSlug.Match(slug);
        if (!fromSlug.Success)
        {
            return null;
        }

        var words = fromSlug.Groups["show"].Value.Replace('-', ' ').Trim();
        return words.Length == 0
            ? null
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words.ToLowerInvariant());
    }

    private static string? IplayerSlug(Episode episode)
    {
        if (episode.Services is not { Count: > 0 })
        {
            return null;
        }

        foreach (var link in episode.Services.Values)
        {
            if (link?.Url is not { IsAbsoluteUri: true } url
                || !SubmitContentClassifier.IsBbcIplayerEpisode(url))
            {
                continue;
            }

            var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 4
                && segments[0].Equals("iplayer", StringComparison.OrdinalIgnoreCase)
                && segments[1].Equals("episode", StringComparison.OrdinalIgnoreCase))
            {
                return segments[3];
            }
        }

        return null;
    }

    private static string? SharedBrand(string cleaned)
    {
        var hyphen = BrandBeforeHyphen.Match(cleaned);
        if (hyphen.Success)
        {
            var brand = hyphen.Groups["brand"].Value.Trim();
            if (brand.Length >= 2 && brand.Length < cleaned.Length)
            {
                return brand;
            }
        }

        var colon = cleaned.IndexOf(':');
        if (colon < 2)
        {
            return null;
        }

        var clause = cleaned[..colon].Trim();
        return clause.Length >= 2 && clause.Length < cleaned.Length ? clause : null;
    }

    private sealed record Row(Guid Id, string Cleaned, string? Series);
}
