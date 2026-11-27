using Api.Dtos.Mapping;
using Api.Models;

namespace Api.Dtos;

/// <summary>Additive kind fields, set only by the GET-by-id endpoints (omitted elsewhere) on the episode read models (<see cref="EpisodeDto"/>, <see cref="PublicEpisodeDto"/>).</summary>
public interface IPlayableKindDto
{
    ContentKindDto? ContentKind { get; set; }
    Guid? ParentId { get; set; }
    string? ParentName { get; set; }
    string? Slug { get; set; }
    string? Path { get; set; }

    /// <summary>Identity image (TMDB) used only when the DTO has no streaming-service image (RPP #1430).</summary>
    Uri? Image { get; set; }

    /// <summary>Other titles a film is known by (RPP #1431). Omitted for other kinds.</summary>
    string[]? AlternativeTitles { get; set; }
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

        dto.ContentKind = kind.ContentKind.ToDto();
        dto.ParentId = kind.ParentId;
        dto.ParentName = kind.ParentName;
        dto.Slug = kind.Slug;
        dto.Path = kind.Path;
        if (dto.Image is null && kind.IdentityImage is not null)
        {
            dto.Image = kind.IdentityImage;
        }

        if (kind.AlternativeTitles is { Length: > 0 })
        {
            dto.AlternativeTitles = kind.AlternativeTitles;
        }

        return dto;
    }
}
