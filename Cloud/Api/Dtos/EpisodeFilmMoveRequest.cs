using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodeFilmMoveRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Answer to an ambiguous-identity prompt: <c>select</c>, <c>continue</c>, or <c>cancel</c>.</summary>
    [JsonPropertyName("identityDecision")]
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    [JsonPropertyName("selectedIdentityKey")]
    public string? SelectedIdentityKey { get; set; }
}
