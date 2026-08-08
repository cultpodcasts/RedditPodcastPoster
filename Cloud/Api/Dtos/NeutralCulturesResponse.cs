using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Languages;

namespace Api.Dtos;

public class NeutralCulturesResponse
{
    [JsonPropertyName("cultures")]
    public required IReadOnlyList<NeutralCultureDto> Cultures { get; init; }

    public class NeutralCultureDto
    {
        [JsonPropertyName("code")]
        public required string Code { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }
    }

    public static NeutralCulturesResponse FromLookup() =>
        new()
        {
            Cultures = NeutralCultureLanguageLookup.ListAll()
                .Select(c => new NeutralCultureDto { Code = c.Code, Name = c.Name })
                .ToList()
        };
}
