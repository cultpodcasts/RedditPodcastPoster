namespace Api.Contracts;

/// <summary>
/// Referenced contract types must live in <c>Api.Dtos</c>. A class, record, struct, or enum
/// outside that namespace fails the export and names the property that pulled it in.
/// Primitives and BCL types are not contract types.
/// </summary>
public static class ContractExportRules
{
    public static readonly string[] FollowNamespacePrefixes = ["Api.Dtos"];
}
