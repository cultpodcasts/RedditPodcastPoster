namespace Api.Models;

public class NewsOrganisationChangeRequest : ServiceIdsChange
{
    /// <summary>Null = unchanged; a list (possibly empty) replaces <c>indexing.additionalYouTubeChannelIds</c> (#1370).</summary>
    public string[]? AdditionalYouTubeChannelIds { get; set; }
}
