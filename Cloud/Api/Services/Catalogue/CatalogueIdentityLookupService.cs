using System.Globalization;
using Api.Dtos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Tmdb.Models;

namespace Api.Services.Catalogue;

/// <summary>
/// Edit-time identity probe: confident → ids; ambiguous → options; no match → message.
/// Film year from the request is forwarded so a single year match can resolve (same as create-time).
/// A film match also proposes the TMDB release so the edit form can show it (RPP #1410).
/// </summary>
public class CatalogueIdentityLookupService(
    ICatalogueIdentityEnricher enricher,
    IIdentityMetadataEnricher identityMetadata,
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
            admission = kind.ToLowerInvariant() switch
            {
                "film" => await enricher.ResolveFilmAsync(
                    title,
                    request.Year,
                    choice,
                    request.SelectedIdentityKey,
                    cancellationToken).ConfigureAwait(false),
                // #1387: edit-time lookup also offers film/documentary hits when the TV authorities have none.
                "tvshow" => await enricher.LookupTvShowAsync(
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
            var ids = ToIdsBody(admission.Ids) ?? new CatalogueIdentityIdsBody();
            if (kind.Equals("film", StringComparison.OrdinalIgnoreCase))
            {
                ids.Release = await ProposeFilmReleaseAsync(ids, cancellationToken).ConfigureAwait(false);
            }

            return new(
                CatalogueIdentityLookupServiceStatus.Ok,
                CatalogueIdentityLookupResponse.Matched(
                    ids,
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

    private async Task<string?> ProposeFilmReleaseAsync(
        CatalogueIdentityIdsBody ids,
        CancellationToken cancellationToken)
    {
        TmdbMovieMetadata? metadata;
        try
        {
            metadata = await identityMetadata
                .FetchFilmAsync(ids.TmdbId, ids.ImdbId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Catalogue identity lookup: film release propose failed. tmdbId={TmdbId} imdbId={ImdbId}",
                ids.TmdbId,
                ids.ImdbId);
            return null;
        }

        if (metadata is null)
        {
            return null;
        }

        if (metadata.ReleaseDate is { } date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (metadata.ReleaseYear is int year and > 0)
        {
            return year.ToString(CultureInfo.InvariantCulture);
        }

        return null;
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
                PageUrl = x.PageUrl?.ToString(),
                ThumbnailUrl = x.ThumbnailUrl?.ToString(),
                Description = x.Description
            }).ToArray()
        };
}
