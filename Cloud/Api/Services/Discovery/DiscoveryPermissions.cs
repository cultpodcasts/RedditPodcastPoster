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

    /// <summary>May review and apply held discovery submissions.</summary>
    public const string Supervise = "supervise";

    /// <summary>Admins may supervise any submission, regardless of supervisor mapping.</summary>
    public const string Admin = "admin";

    /// <summary>
    /// Optional Auth0 custom claim on a supervised curator's token listing the subjects of their supervisors.
    /// Captured onto the held submission; when present, only those supervisors (or an admin) may review it.
    /// </summary>
    public const string SupervisorsClaim = "https://api.cultpodcasts.com/supervisors";
}
