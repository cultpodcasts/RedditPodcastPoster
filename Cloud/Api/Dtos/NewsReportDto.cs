using System.Text.Json.Serialization;

namespace Api.Dtos;

public class NewsReportDto : PlayableFieldsDto
{
    [JsonPropertyName("newsOrganisationId")]
    public Guid NewsOrganisationId { get; set; }

    [JsonPropertyName("newsOrganisationName")]
    public string? NewsOrganisationName { get; set; }
}
