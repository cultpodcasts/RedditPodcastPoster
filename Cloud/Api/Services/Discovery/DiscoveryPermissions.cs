namespace Api.Services.Discovery;

/// <summary>Auth0 permissions used by discovery curation (JWT <c>permissions</c> claim).</summary>
public static class DiscoveryPermissions
{
    public const string Curate = "curate";

    /// <summary>
    /// Marks a curator whose discovery submits are held for a supervisor instead of being applied.
    /// Takes precedence over <see cref="Curate"/> on discovery submit.
    /// </summary>
    public const string CurateSupervised = "curate:supervised";

    /// <summary>May review and apply held discovery submissions (endpoints land in a later PR).</summary>
    public const string Supervise = "supervise";
}
