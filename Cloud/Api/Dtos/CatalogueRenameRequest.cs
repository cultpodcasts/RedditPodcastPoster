using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Body of <c>POST newsorganisation|tvshow|film/name/{identifier}</c>; mirrors <see cref="PodcastRenameRequest"/>.</summary>
public class CatalogueRenameRequest
{
    [JsonPropertyName("newName")]
    public required string NewName { get; set; }
}
