using Api.Dtos.Extensions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.People.Services;

namespace Api.Dtos.Mapping;

/// <summary>
/// Resolves guest people and title/description suggestions for curator playable GETs
/// (TV episode, news report, film), matching <see cref="EpisodeDtoMapper"/> when
/// <c>includeGuestSuggestions</c> is true.
/// </summary>
public interface IPlayableGuestDtoEnricher
{
    Task EnrichAsync(
        IPlayableGuestFieldsDto dto,
        string title,
        string description,
        string? language,
        string[]? guests,
        CancellationToken cancellationToken = default);
}

/// <summary>DTO surface that carries guests plus optional people / suggestions on GET.</summary>
public interface IPlayableGuestFieldsDto
{
    string[]? Guests { get; set; }
    IList<PersonDto>? GuestPeople { get; set; }
    IList<EpisodeDto.PersonMatch>? GuestSuggestions { get; set; }
}

public sealed class PlayableGuestDtoEnricher(IPersonService personService) : IPlayableGuestDtoEnricher
{
    public async Task EnrichAsync(
        IPlayableGuestFieldsDto dto,
        string title,
        string description,
        string? language,
        string[]? guests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (guests is { Length: > 0 })
        {
            var guestPeople = await personService.GetByNames(guests);
            dto.GuestPeople = guestPeople.Select(x => x.ToDto()).ToList();
        }

        var probe = new Episode
        {
            Title = title ?? string.Empty,
            Description = description ?? string.Empty,
            Language = language,
            Guests = guests
        };
        var selectedNames = guests?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var suggestions = await personService.MatchEpisode(probe, withDescription: true);
        dto.GuestSuggestions = suggestions
            .Where(x => !selectedNames.Contains(x.Person.Name))
            .Select(EpisodeDtoMapper.ToPersonMatch)
            .ToList();
    }
}
