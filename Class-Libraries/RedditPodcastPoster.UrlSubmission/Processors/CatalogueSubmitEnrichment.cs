using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

/// <summary>
/// The metadata enrichment a new podcast episode gets on submit (<see cref="PodcastProcessor"/>), for a film, TV
/// episode or news report that <see cref="CatalogueKindSubmitter"/> is about to save:
/// <list type="number">
/// <item>a blank description is filled from the other resolved services (<see cref="IDescriptionHelper"/>);</item>
/// <item>subjects are matched with <see cref="ISubjectEnrichmentOptionsFactory"/> options built from the parent's
/// transient podcast (default subject, ignored and associated-ignored subjects, description regex, language ignore
/// list) — a film is its own parent;</item>
/// <item>guests are matched; additions and low-confidence suggestions are returned as
/// <see cref="SubmitEpisodeDetails"/> for the submit response.</item>
/// </list>
/// The item's text is projected onto a transient <see cref="Episode"/> carrying the item's id, the enrichers run on
/// it, and subjects, matches and guests are copied back.
/// </summary>
public interface ICatalogueSubmitEnrichment
{
    Task<SubmitEpisodeDetails> Enrich(Film film, CategorisedItem categorisedItem);

    Task<SubmitEpisodeDetails> Enrich(
        Playable playable,
        Publisher parent,
        CatalogueIndexingSource? parentIndexing,
        CategorisedItem categorisedItem);
}

public sealed class CatalogueSubmitEnrichment(
    IDescriptionHelper descriptionHelper,
    ISubjectEnricher subjectEnricher,
    ISubjectEnrichmentOptionsFactory subjectEnrichmentOptionsFactory,
    IEpisodeGuestEnricher guestEnricher,
    ILogger<CatalogueSubmitEnrichment> logger) : ICatalogueSubmitEnrichment
{
    public async Task<SubmitEpisodeDetails> Enrich(Film film, CategorisedItem categorisedItem)
    {
        ArgumentNullException.ThrowIfNull(film);
        if (string.IsNullOrWhiteSpace(film.Description))
        {
            film.Description = descriptionHelper.EnrichMissingDescription(categorisedItem) ?? string.Empty;
        }

        var (projection, details) = await EnrichProjection(
            film.Id, film.Name, film.Description, film.Language, film.Services,
            new CatalogueIndexingSource().ToTransientPodcast(film), "film");
        film.Subjects = [.. projection.Subjects];
        film.Matches = [.. projection.Matches];
        film.Guests = projection.Guests is null ? null : [.. projection.Guests];
        return details;
    }

    public async Task<SubmitEpisodeDetails> Enrich(
        Playable playable,
        Publisher parent,
        CatalogueIndexingSource? parentIndexing,
        CategorisedItem categorisedItem)
    {
        ArgumentNullException.ThrowIfNull(playable);
        ArgumentNullException.ThrowIfNull(parent);
        if (string.IsNullOrWhiteSpace(playable.Description))
        {
            playable.Description = descriptionHelper.EnrichMissingDescription(categorisedItem) ?? string.Empty;
        }

        var (projection, details) = await EnrichProjection(
            playable.Id, playable.Title, playable.Description, playable.Language, playable.Services,
            (parentIndexing ?? new CatalogueIndexingSource()).ToTransientPodcast(parent), playable.GetType().Name);
        playable.Subjects = [.. projection.Subjects];
        playable.Matches = [.. projection.Matches];
        playable.Guests = projection.Guests is null ? null : [.. projection.Guests];
        return details;
    }

    private async Task<(Episode Projection, SubmitEpisodeDetails Details)> EnrichProjection(
        Guid id,
        string? title,
        string? description,
        string? language,
        Dictionary<string, ServiceLink>? services,
        Podcast transient,
        string kind)
    {
        var projection = new Episode
        {
            Id = id,
            Title = title ?? string.Empty,
            Description = description ?? string.Empty,
            // As Episode.SetPodcastProperties(inheritLanguageIfUnset: true): an item with no language of its own
            // reads as its parent's, so the per-language subject ignore list matches the podcast path.
            Language = string.IsNullOrWhiteSpace(language) ? transient.Language : language,
            // The item's platform links (cloned, so nothing on the projection is shared back), so the submit
            // response's Spotify / Apple / YouTube flags and extra service keys describe the item.
            Services = services?.ToDictionary(
                pair => pair.Key,
                pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language })
        };
        var subjectsResult = await subjectEnricher.EnrichSubjects(
            projection,
            await subjectEnrichmentOptionsFactory.CreateAsync(transient, projection));
        var guestsResult = await guestEnricher.EnrichGuests(projection);

        if (!projection.Subjects.Any())
        {
            logger.LogError(
                "{method}: No subjects applied to new {kind} '{title}' ({id}) under '{parentName}' ({parentId}). Configured default-subject: '{defaultSubject}'.",
                nameof(Enrich), kind, projection.Title, id, transient.Name, transient.Id,
                transient.DefaultSubject ?? "(none)");
        }
        else if (!string.IsNullOrWhiteSpace(transient.DefaultSubject) &&
                 projection.Subjects.Count == 1 &&
                 string.Equals(projection.Subjects[0], transient.DefaultSubject, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "{method}: No specific subjects matched for new {kind} '{title}' ({id}) under '{parentName}' ({parentId}); applied default-subject '{defaultSubject}'.",
                nameof(Enrich), kind, projection.Title, id, transient.Name, transient.Id, transient.DefaultSubject);
        }

        return (projection, SubmitEpisodeDetails.FromEpisode(
            projection,
            subjectsResult.Additions,
            guestsResult.Additions,
            guestsResult.SkippedLowConfidence));
    }
}
