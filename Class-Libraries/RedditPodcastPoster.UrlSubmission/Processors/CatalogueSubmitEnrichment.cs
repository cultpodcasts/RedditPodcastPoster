using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.Subjects.Enrichers;

namespace RedditPodcastPoster.UrlSubmission.Processors;

/// <summary>
/// The text enrichment a new podcast episode gets on submit (subjects with their matches, then guests), for a film,
/// TV episode or news report that <see cref="CatalogueKindSubmitter"/> is about to save. One shared step for every
/// kind: the item's title, description and language are projected onto a transient <see cref="Episode"/>, the same
/// <see cref="ISubjectEnricher"/> / <see cref="IEpisodeGuestEnricher"/> the podcast path uses run on it (no podcast
/// options, as for an episode of a newly created podcast), and the results are copied back.
/// </summary>
public interface ICatalogueSubmitEnrichment
{
    Task Enrich(Film film);
    Task Enrich(Playable playable);
}

public sealed class CatalogueSubmitEnrichment(
    ISubjectEnricher subjectEnricher,
    IEpisodeGuestEnricher guestEnricher) : ICatalogueSubmitEnrichment
{
    public async Task Enrich(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        var projection = await EnrichProjection(film.Name, film.Description, film.Language);
        film.Subjects = [.. projection.Subjects];
        film.Matches = [.. projection.Matches];
        film.Guests = projection.Guests is null ? null : [.. projection.Guests];
    }

    public async Task Enrich(Playable playable)
    {
        ArgumentNullException.ThrowIfNull(playable);
        var projection = await EnrichProjection(playable.Title, playable.Description, playable.Language);
        playable.Subjects = [.. projection.Subjects];
        playable.Matches = [.. projection.Matches];
        playable.Guests = projection.Guests is null ? null : [.. projection.Guests];
    }

    private async Task<Episode> EnrichProjection(string? title, string? description, string? language)
    {
        var projection = new Episode
        {
            Title = title ?? string.Empty,
            Description = description ?? string.Empty,
            Language = language
        };
        await subjectEnricher.EnrichSubjects(projection);
        await guestEnricher.EnrichGuests(projection);
        return projection;
    }
}
