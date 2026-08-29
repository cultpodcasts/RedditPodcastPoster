namespace PublishR2;

public class R2PublishRequest
{
    public required R2PublishTarget Target { get; init; }
}

public enum R2PublishTarget
{
    Languages,
    People,
    SearchSuggestions,
    Subjects,
    Homepage,
    /// <summary>
    /// Edge lookup JSON only: languages, people, search-suggestions, subjects.
    /// Does not include homepage, homepage-ssr, feed, or discovery-info.
    /// </summary>
    Lookups,
    All
}
