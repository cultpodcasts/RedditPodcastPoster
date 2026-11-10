namespace Api.Models;

/// <summary>
/// Target of a move to a news organisation. Exactly one of <see cref="NewsOrganisationId"/> or
/// <see cref="NewsOrganisationName"/> is given.
/// </summary>
public class EpisodeNewsMoveRequest
{
    /// <summary>An existing news organisation, by id.</summary>
    public Guid? NewsOrganisationId { get; set; }

    /// <summary>
    /// A news organisation by name (case-insensitive, trimmed). Only news organisations are matched, so a podcast with
    /// the same name does not count. With <see cref="CreateOrganisation"/> a missing organisation is created.
    /// </summary>
    public string? NewsOrganisationName { get; set; }

    /// <summary>
    /// With <see cref="NewsOrganisationName"/>: create the organisation when no news organisation has the name. When
    /// one already has it, the episodes join that organisation and <see cref="Publisher"/> is not applied.
    /// </summary>
    public bool CreateOrganisation { get; set; }

    /// <summary>Publisher of a newly created organisation. Only valid with <see cref="CreateOrganisation"/>.</summary>
    public string? Publisher { get; set; }
}
