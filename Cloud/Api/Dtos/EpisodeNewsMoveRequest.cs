using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodeNewsMoveRequest
{
    [JsonPropertyName("newsOrganisationId")]
    public Guid? NewsOrganisationId { get; set; }

    [JsonPropertyName("newsOrganisationName")]
    public string? NewsOrganisationName { get; set; }

    [JsonPropertyName("createOrganisation")]
    public bool? CreateOrganisation { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }
}
