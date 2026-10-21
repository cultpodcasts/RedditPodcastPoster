using Api.Models;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.News;

/// <summary>
/// Curator GET/PATCH for a news organisation. Mirrors <c>TvShowService</c> for lookup (id, or a unique name).
/// The editable set is the publisher fields a news organisation carries (social handles, hashtags, terms,
/// subjects, language). Name and removed are not editable here: a rename moves the public <c>/news/{name}</c>
/// route and search series name, and removal must also clear search documents, so both need their own flows.
/// </summary>
public class NewsOrganisationService(
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    ILogger<NewsOrganisationService> logger) : INewsOrganisationService
{
    public async Task<NewsOrganisationGetResult> GetAsync(string identifier, CancellationToken cancellationToken)
    {
        try
        {
            if (Guid.TryParse(identifier, out var id))
            {
                var byId = await newsOrganisationRepository.GetNewsOrganisation(id);
                return byId is null
                    ? new NewsOrganisationGetResult(NewsOrganisationGetStatus.NotFound)
                    : new NewsOrganisationGetResult(NewsOrganisationGetStatus.Found, byId);
            }

            var name = PodcastRouteNameNormalizer.Normalize(identifier);
            var matches = await PublisherNameAttachLookup.FindByName(newsOrganisationRepository, name, cancellationToken);
            if (matches.Count == 0)
            {
                return new NewsOrganisationGetResult(NewsOrganisationGetStatus.NotFound);
            }

            if (matches.Count == 1)
            {
                return new NewsOrganisationGetResult(NewsOrganisationGetStatus.Found, matches[0]);
            }

            return new NewsOrganisationGetResult(
                NewsOrganisationGetStatus.Conflict,
                AmbiguousIds: matches.Select(organisation => organisation.Id).ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get news organisation '{identifier}'.", nameof(GetAsync), identifier);
            return new NewsOrganisationGetResult(NewsOrganisationGetStatus.Failed);
        }
    }

    public async Task<NewsOrganisationUpdateResult> UpdateAsync(
        NewsOrganisationChangeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            var organisation = await newsOrganisationRepository.GetNewsOrganisation(request.NewsOrganisationId);
            if (organisation is null)
            {
                return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.NotFound);
            }

            Apply(organisation, request.Change);
            await newsOrganisationRepository.Save(organisation);

            if (request.Change.SearchTerms == null && request.Change.Language == null)
            {
                return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Accepted);
            }

            // Reports carry the organisation's search terms and language (publisherSearchTerms /
            // publisherLanguage) for search. Saving a changed report also bumps its _ts, so the
            // newsreport search indexer picks the new values up on its next run.
            var reports = await newsReportRepository.GetByNewsOrganisationId(organisation.Id).ToListAsync(cancellationToken);
            var changed = reports.Where(report => report.SetNewsOrganisationProperties(organisation).Updated).ToArray();
            if (changed.Length > 0)
            {
                await newsReportRepository.Save(changed);
                logger.LogInformation(
                    "{method}: Updated {count} news report(s) of news organisation '{id}' with its new properties.",
                    nameof(UpdateAsync), changed.Length, organisation.Id);
            }

            return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update news organisation '{id}'.", nameof(UpdateAsync),
                request.NewsOrganisationId);
            return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Failed);
        }
    }

    /// <summary>Same null / empty semantics as <c>PodcastChangeApplier</c>.</summary>
    internal static void Apply(NewsOrganisation organisation, NewsOrganisationChangeRequest change)
    {
        if (change.Language != null)
        {
            organisation.Language = EpisodeChangeApplier.NormaliseEpisodeLanguage(change.Language);
        }

        if (change.TwitterHandle != null)
        {
            organisation.TwitterHandle = change.TwitterHandle.Trim();
        }

        if (change.BlueskyHandle != null)
        {
            organisation.BlueskyHandle = string.IsNullOrWhiteSpace(change.BlueskyHandle)
                ? null
                : change.BlueskyHandle.Trim();
        }

        if (change.HashTag != null)
        {
            organisation.HashTag = string.IsNullOrWhiteSpace(change.HashTag) ? null : change.HashTag.Trim();
        }

        if (change.EnrichmentHashTags != null)
        {
            organisation.EnrichmentHashTags = change.EnrichmentHashTags.Length == 0
                ? null
                : change.EnrichmentHashTags.Select(x => x.Trim()).ToArray();
        }

        if (change.SearchTerms != null)
        {
            organisation.SearchTerms = string.IsNullOrWhiteSpace(change.SearchTerms) ? null : change.SearchTerms;
        }

        if (change.KnownTerms != null)
        {
            organisation.KnownTerms = change.KnownTerms.Length > 0 ? change.KnownTerms : null;
        }

        if (change.DefaultSubject != null)
        {
            organisation.DefaultSubject = change.DefaultSubject == string.Empty ? null : change.DefaultSubject;
        }

        if (change.IgnoredSubjects != null)
        {
            organisation.IgnoredSubjects = change.IgnoredSubjects;
        }

        if (change.IgnoredAssociatedSubjects != null)
        {
            organisation.IgnoredAssociatedSubjects = change.IgnoredAssociatedSubjects;
        }
    }
}
