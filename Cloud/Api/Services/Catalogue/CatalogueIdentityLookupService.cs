using Api.Dtos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Models.TvShows;

namespace Api.Services.Catalogue;

/// <summary>
/// Edit-time identity probe: confident → ids; ambiguous → options (never year-narrowed or otherwise
/// auto-picked); no match → message. Create-time submit may still auto-narrow film by year.
/// </summary>
public class CatalogueIdentityLookupService(
    ICatalogueIdentityEnricher enricher,
    ILogger<CatalogueIdentityLookupService> logger) : ICatalogueIdentityLookupService
{
    public async Task<CatalogueIdentityLookupServiceResult> LookupAsync(
        CatalogueIdentityLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Kind) || string.IsNullOrWhiteSpace(request.Title))
        {
            return new(CatalogueIdentityLookupServiceStatus.BadRequest, null);
        }

        var kind = request.Kind.Trim();
        var title = request.Title.Trim();
        var choice = CatalogueIdentityChoices.Parse(request.IdentityDecision);
        if (choice is CatalogueIdentityChoice.Continue or CatalogueIdentityChoice.Cancel)
        {
            // Edit-time continue/cancel means leave the form alone — no second round-trip needed.
            return new(
                CatalogueIdentityLookupServiceStatus.Ok,
                CatalogueIdentityLookupResponse.NoMatch(
                    "Identity fields left unchanged."));
        }

        CatalogueIdentityAdmission admission;
        try
        {
            // Pass year: null for film so SeveralProgrammes always holds at edit-time
            // (create-time ResolveFilmAsync may still auto-narrow by year).
            admission = kind.ToLowerInvariant() switch
            {
                "film" => await enricher.ResolveFilmAsync(
                    title,
                    year: null,
                    choice,
                    request.SelectedIdentityKey,
                    cancellationToken).ConfigureAwait(false),
                "tvshow" => await enricher.ResolveTvShowAsync(
                    title,
                    choice,
                    request.SelectedIdentityKey,
                    cancellationToken).ConfigureAwait(false),
                "tvshowepisode" => await enricher.ResolveTvShowEpisodeAsync(
                    BuildShow(request),
                    title,
                    choice,
                    request.SelectedIdentityKey,
                    cancellationToken).ConfigureAwait(false),
                _ => null!
            };
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Catalogue identity lookup failed. kind={Kind} title={Title}", kind, title);
            return new(
                CatalogueIdentityLookupServiceStatus.Ok,
                CatalogueIdentityLookupResponse.NoMatch(
                    "Identity lookup failed. Fields left unchanged."));
        }

        if (admission is null)
        {
            return new(CatalogueIdentityLookupServiceStatus.BadRequest, null);
        }

        if (admission.Hold && admission.Prompt is { Candidates.Count: > 0 } prompt)
        {
            return new(
                CatalogueIdentityLookupServiceStatus.Ok,
                CatalogueIdentityLookupResponse.Ambiguous(ToDecision(prompt)));
        }

        if (HasIds(admission.Ids) || HasIds(admission.ParentIds))
        {
            return new(
                CatalogueIdentityLookupServiceStatus.Ok,
                CatalogueIdentityLookupResponse.Matched(
                    ToIdsBody(admission.Ids) ?? new CatalogueIdentityIdsBody(),
                    ToIdsBody(admission.ParentIds)));
        }

        return new(
            CatalogueIdentityLookupServiceStatus.Ok,
            CatalogueIdentityLookupResponse.NoMatch(
                $"No confident {kind} identity match for “{title}”. Fields left unchanged."));
    }

    private static TvShow BuildShow(CatalogueIdentityLookupRequest request)
    {
        var name = string.IsNullOrWhiteSpace(request.ShowName) ? request.Title.Trim() : request.ShowName.Trim();
        var show = new TvShow(name);
        if (!string.IsNullOrWhiteSpace(request.ShowImdbId))
        {
            show.ImdbId = request.ShowImdbId.Trim();
        }

        if (request.ShowTmdbId is > 0)
        {
            show.TmdbId = request.ShowTmdbId;
        }

        if (request.ShowTvdbId is > 0)
        {
            show.TvdbId = request.ShowTvdbId;
        }

        return show;
    }

    private static bool HasIds(CatalogueAuthorityIds? ids) =>
        ids is not null &&
        (!string.IsNullOrWhiteSpace(ids.ImdbId) || ids.TmdbId is > 0 || ids.TvdbId is > 0);

    private static CatalogueIdentityIdsBody? ToIdsBody(CatalogueAuthorityIds? ids)
    {
        if (!HasIds(ids))
        {
            return null;
        }

        return new CatalogueIdentityIdsBody
        {
            ImdbId = ids!.ImdbId,
            TmdbId = ids.TmdbId,
            TvdbId = ids.TvdbId
        };
    }

    private static SubmitUrlResponse.IdentityDecisionBody ToDecision(CatalogueIdentityHold hold) =>
        new()
        {
            Target = hold.Target,
            Query = hold.Query,
            Candidates = hold.Candidates.Select(x => new SubmitUrlResponse.IdentityCandidateBody
            {
                Key = x.Key,
                Title = x.Title,
                Year = x.Year,
                Kind = x.Kind,
                Source = x.Source,
                TmdbId = x.TmdbId,
                TvdbId = x.TvdbId,
                PageUrl = x.PageUrl?.ToString()
            }).ToArray()
        };
}
