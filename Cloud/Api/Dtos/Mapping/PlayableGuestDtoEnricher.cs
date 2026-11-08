using Api.Dtos.Extensions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.People.Services;

namespace Api.Dtos.Mapping;

/// <summary>
/// Resolves guest people and title/description suggestions for curator playable GETs
/// (episode, TV episode, news report, film). Single path for guestPeople / guestSuggestions
/// so episode and catalogue playables cannot drift.
/// </summary>
public interface IPlayableGuestDtoEnricher
{
    /// <param name="includeGuestSuggestions">
    /// When false, only resolves <see cref="IPlayableGuestFieldsDto.GuestPeople"/>
    /// (list/outgoing episode projections). Curator single-resource GETs pass true.
    /// </param>
    Task EnrichAsync(
        IPlayableGuestFieldsDto dto,
        string title,
        string description,
        string? language,
        string[]? guests,
        bool includeGuestSuggestions = true,
        CancellationToken cancellationToken = default);
}

/// <summary>DTO surface that carries guests plus optional people / suggestions on GET.</summary>
public interface IPlayableGuestFieldsDto
{
    string[]? Guests { get; set; }

    /// <summary>Resolved people for <see cref="Guests"/> (curator edit).</summary>
    IList<PersonDto>? GuestPeople { get; set; }

    /// <summary>People matched from title/description who are not already in <see cref="Guests"/>.</summary>
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
        bool includeGuestSuggestions = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (guests is { Length: > 0 })
        {
            var guestPeople = await personService.GetByNames(guests);
            dto.GuestPeople = guestPeople.Select(x => x.ToDto()).ToList();
        }

        if (!includeGuestSuggestions)
        {
            return;
        }

        var probe = new Episode
        {
            Title = title,
            Description = description,
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
