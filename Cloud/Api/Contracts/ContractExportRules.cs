namespace Api.Contracts;

/// <summary>
/// Which referenced types the contract generator may follow. Enums are always followed.
/// A type outside these prefixes is exported only when it carries <see cref="ExportContractAttribute"/>.
/// </summary>
public static class ContractExportRules
{
    public static readonly string[] FollowNamespacePrefixes =
    [
        "Api.Dtos",
        "Api.Models",
        "RedditPodcastPoster.Models"
    ];
}
