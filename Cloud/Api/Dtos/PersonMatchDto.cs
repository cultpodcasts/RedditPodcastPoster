using System.Text.Json.Serialization;

namespace Api.Dtos;

public class PersonMatchDto
{
    [JsonPropertyName("person")]
    public required PersonDto Person { get; set; }

    [JsonPropertyName("matchResults")]
    public required PersonMatchResultDto[] MatchResults { get; set; }
}

public class PersonMatchResultDto
{
    [JsonPropertyName("term")]
    public required string Term { get; set; }

    [JsonPropertyName("matches")]
    public required int Matches { get; set; }
}
