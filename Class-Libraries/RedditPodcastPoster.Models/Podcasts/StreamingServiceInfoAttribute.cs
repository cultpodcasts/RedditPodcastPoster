namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Catalog metadata for a <see cref="StreamingService"/> wire destination.
/// Wire JSON key comes from <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/>
/// or camelCase of the enum member name.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class StreamingServiceInfoAttribute(
    string displayName,
    string icon,
    bool wideImage,
    params string[] hosts) : Attribute
{
    public string DisplayName { get; } = displayName;
    public string Icon { get; } = icon;
    public bool WideImage { get; } = wideImage;
    public IReadOnlyList<string> Hosts { get; } = hosts;
}
