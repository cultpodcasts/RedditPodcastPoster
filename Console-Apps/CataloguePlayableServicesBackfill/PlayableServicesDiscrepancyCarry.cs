using RedditPodcastPoster.Models.Services;

namespace CataloguePlayableServicesBackfill;

public sealed record ServiceFieldChange(
    string Key,
    string Field,
    string? Before,
    string? After);

public sealed record PlayableServicesCarryResult(
    Dictionary<string, ServiceLink>? Services,
    IReadOnlyList<ServiceFieldChange> Changes)
{
    public bool HasChanges => Changes.Count > 0;
}

/// <summary>
/// Diffs backup-derived services against a live playable Services map and carries
/// discrepant non-null url/image values from backup onto the live map.
/// Null backup fields never wipe live values.
/// </summary>
public static class PlayableServicesDiscrepancyCarry
{
    public static PlayableServicesCarryResult Apply(
        Dictionary<string, ServiceLink>? live,
        IReadOnlyDictionary<string, ServiceLink> backup)
    {
        ArgumentNullException.ThrowIfNull(backup);

        var working = CloneServices(live) ?? new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        var changes = new List<ServiceFieldChange>();

        foreach (var (key, backupLink) in backup)
        {
            working.TryGetValue(key, out var liveLink);
            liveLink ??= new ServiceLink();

            // Only carry when backup has a concrete value that differs from live.
            var urlShouldCarry = backupLink.Url is not null && !UriEquals(liveLink.Url, backupLink.Url);
            var imageShouldCarry = backupLink.Image is not null && !UriEquals(liveLink.Image, backupLink.Image);
            if (!urlShouldCarry && !imageShouldCarry)
            {
                continue;
            }

            if (urlShouldCarry)
            {
                changes.Add(new ServiceFieldChange(
                    key,
                    "url",
                    FormatUri(liveLink.Url),
                    FormatUri(backupLink.Url)));
                liveLink.Url = backupLink.Url;
            }

            if (imageShouldCarry)
            {
                changes.Add(new ServiceFieldChange(
                    key,
                    "image",
                    FormatUri(liveLink.Image),
                    FormatUri(backupLink.Image)));
                liveLink.Image = backupLink.Image;
            }

            if (backupLink.Language is not null)
            {
                liveLink.Language = backupLink.Language;
            }

            working[key] = liveLink;
        }

        Dictionary<string, ServiceLink>? result = working.Count == 0 ? null : working;
        return new PlayableServicesCarryResult(result, changes);
    }

    public static Dictionary<string, ServiceLink>? CloneServices(Dictionary<string, ServiceLink>? services)
    {
        if (services is null)
        {
            return null;
        }

        return services.ToDictionary(
            pair => pair.Key,
            pair => new ServiceLink
            {
                Url = pair.Value.Url,
                Image = pair.Value.Image,
                Language = pair.Value.Language
            },
            StringComparer.Ordinal);
    }

    public static bool UriEquals(Uri? left, Uri? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.AbsoluteUri, right.AbsoluteUri, StringComparison.Ordinal);
    }

    private static string? FormatUri(Uri? uri) => uri?.AbsoluteUri;
}
