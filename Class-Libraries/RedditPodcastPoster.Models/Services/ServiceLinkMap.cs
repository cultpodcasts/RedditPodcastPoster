namespace RedditPodcastPoster.Models.Services;

/// <summary>Helpers for a stored <c>services</c> map, which can hold a <c>"key": null</c> entry (#1508).</summary>
public static class ServiceLinkMap
{
    /// <summary>
    /// Deep-copies the links, leaving out null entries: a null entry is not a usable link, and copying it
    /// would throw. Returns null for a null map.
    /// </summary>
    public static Dictionary<string, ServiceLink>? CloneWithoutNulls(
        IEnumerable<KeyValuePair<string, ServiceLink>>? services) =>
        services?
            .Where(pair => pair.Value is not null)
            .ToDictionary(
                pair => pair.Key,
                pair => new ServiceLink
                {
                    Url = pair.Value.Url,
                    Image = pair.Value.Image,
                    Language = pair.Value.Language
                });
}
