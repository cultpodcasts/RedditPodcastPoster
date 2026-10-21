namespace Api.Models;

public class PersonChangeRequest
{
    public Guid? Id { get; set; }

    public string? Name { get; set; }

    public string? SortName { get; set; }

    /// <summary>
    /// Organization/entity flag. On create/GET always set; on PATCH omit (null) to leave unchanged.
    /// </summary>
    public bool? IsOrganization { get; set; }

    public string[]? Aliases { get; set; }

    public string? TwitterHandle { get; set; }

    public string? BlueskyHandle { get; set; }
}
