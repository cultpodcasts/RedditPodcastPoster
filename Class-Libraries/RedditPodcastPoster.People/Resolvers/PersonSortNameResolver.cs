using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.People;

namespace RedditPodcastPoster.People.Resolvers;

/// <summary>
/// Mirrors website <c>person-sort.ts</c> / PeopleReviewer <c>person-sort.js</c>.
/// Persist <c>sortName</c> when the effective key differs from the last-token surname default
/// (org full-name and manual overrides). Omit only when effective key equals that default.
/// Org/entity sort keys use <see cref="StripLeadingThe"/> so "The Lead CNN" sorts as "Lead CNN".
/// </summary>
public static class PersonSortNameResolver
{
    private static readonly string[] OrgSortKeywords =
    [
        "podcast", "news", "morning", "cnn", "channel", "fm", "am", "tv", "radio",
        "network", "show", "official", "bbc", "nbc", "abc", "cbs", "msnbc", "fox",
        "sky", "media", "press", "times", "post", "journal", "gazette", "tribune",
        "herald", "daily", "weekly", "magazine", "inc", "llc", "ltd", "corp",
        "company", "foundation", "institute", "ministry", "church", "temple",
        "university", "college", "school", "association", "society", "committee",
        "commission", "agency", "bureau", "department", "office", "group",
        "collective", "productions", "entertainment", "studios", "records"
    ];

    private static readonly Regex OrgKeywordPattern = BuildOrgKeywordPattern();

    /// <summary>
    /// Strips a leading "The " (case-insensitive) used for corp/entity sort keys.
    /// Display <see cref="Person.Name"/> is unchanged; only the sort key drops the article.
    /// </summary>
    public static string StripLeadingThe(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length >= 4 &&
            trimmed.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[4..].TrimStart();
        }

        return trimmed;
    }

    public static bool LooksLikeOrganization(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (OrgKeywordPattern.IsMatch(trimmed))
        {
            return true;
        }

        var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 5)
        {
            return true;
        }

        if (parts.Length >= 2 &&
            string.Equals(trimmed, trimmed.ToUpperInvariant(), StringComparison.Ordinal) &&
            trimmed.Any(char.IsAsciiLetterUpper))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Suggested sort name: orgs ΓåÆ <see cref="StripLeadingThe"/>(full name); else last token.
    /// </summary>
    public static string GuessSortName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return LooksLikeOrganization(trimmed)
            ? StripLeadingThe(trimmed)
            : Person.DeriveSortKeyFromName(trimmed);
    }

    /// <summary>
    /// Value to store on <see cref="Person.SortName"/>.
    /// <list type="bullet">
    /// <item>Explicit <paramref name="isOrganization"/> or heuristic org: persist <c>StripLeadingThe(Name)</c> when Γëá last-token.</item>
    /// <item>Manual overrides kept (leading "The " stripped when the value is an org full-name key).</item>
    /// <item>Null only when effective key equals the last whitespace token of Name.</item>
    /// </list>
    /// </summary>
    public static string? ResolveForPersist(string? name, string? sortName, bool isOrganization = false)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            return null;
        }

        var lastToken = Person.DeriveSortKeyFromName(trimmedName);
        var isOrg = isOrganization || LooksLikeOrganization(trimmedName);
        var orgKey = isOrg ? StripLeadingThe(trimmedName) : null;

        // Curator flagged organization: always use the full-name org key (ignore stale surname seed).
        if (isOrganization && orgKey is not null)
        {
            return string.Equals(orgKey, lastToken, StringComparison.Ordinal) ? null : orgKey;
        }

        string effective;
        if (!string.IsNullOrWhiteSpace(sortName))
        {
            effective = sortName.Trim();
            if (isOrg && orgKey is not null)
            {
                // Full-name (with/without The) or any leading-The org key ΓåÆ canonical org sort key
                if (string.Equals(effective, trimmedName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(StripLeadingThe(effective), orgKey, StringComparison.OrdinalIgnoreCase) ||
                    effective.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
                {
                    effective = orgKey;
                }
            }
        }
        else if (isOrg && orgKey is not null)
        {
            effective = orgKey;
        }
        else
        {
            effective = lastToken;
        }

        return string.Equals(effective, lastToken, StringComparison.Ordinal) ? null : effective;
    }

    private static Regex BuildOrgKeywordPattern()
    {
        var alternation = string.Join("|", OrgSortKeywords.Select(Regex.Escape));
        return new Regex($@"\b(?:{alternation})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
