namespace Api.Models;

/// <summary>
/// Rename a news organisation, TV show or film. <see cref="Identifier"/> is the entity's guid, or its route-form
/// name (resolved like the kind's GET, so an ambiguous name is a 409 with the candidate ids).
/// </summary>
public record CatalogueRenameCommand(string Identifier, string NewName);
