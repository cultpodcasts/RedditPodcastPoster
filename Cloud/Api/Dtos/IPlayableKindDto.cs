using Api.Models;

namespace Api.Dtos;

/// <summary>Additive kind fields, set only by the GET-by-id endpoints (omitted elsewhere) on the episode read models (<see cref="EpisodeDto"/>, <see cref="PublicEpisodeDto"/>).</summary>
public interface IPlayableKindDto
{
    string? ContentKind { get; set; }
    Guid? ParentId { get; set; }
    string? ParentName { get; set; }
    string? Slug { get; set; }
    string? Path { get; set; }
}

public static class PlayableKindDtoExtensions
{
    public static async Task<T> WithKindAsync<T>(this Task<T> dto, PlayableKind? kind) where T : IPlayableKindDto =>
        (await dto).WithKind(kind);

    /// <summary>A null kind leaves the fields unset (and so omitted).</summary>
    public static T WithKind<T>(this T dto, PlayableKind? kind) where T : IPlayableKindDto
    {
        if (kind is null)
        {
            return dto;
        }

        dto.ContentKind = kind.ContentKind;
        dto.ParentId = kind.ParentId;
        dto.ParentName = kind.ParentName;
        dto.Slug = kind.Slug;
        dto.Path = kind.Path;
        return dto;
    }
}
