using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// Attaches a podcast episode's YouTube upload (and any other non-conflicting service links) to an existing
/// catalogue item as an additional YouTube link (RPP #1411), then deletes the episode row and points its short
/// link at the owning item. A Film target is the same merge as Move to an existing film.
/// </summary>
public class YouTubeAlternateAttachService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    INewsReportRepository newsReportRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    IEpisodeServiceMaterialiser episodeServiceMaterialiser,
    IEpisodeFilmMoveService episodeFilmMoveService,
    IUrlMembershipLookup urlMembershipLookup,
    ILogger<YouTubeAlternateAttachService> logger) : IYouTubeAlternateAttachService
{
    private const int MaxSaveAttempts = 3;

    public async Task<EpisodeYouTubeAlternateAttachResult> AttachAsync(
        EpisodeYouTubeAlternateAttachCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            if (command.Request.TargetKind == ContentKind.Film)
            {
                return await AttachToFilm(command, cancellationToken);
            }

            var podcast = await podcastRepository.GetPodcast(command.PodcastId);
            var episode = podcast is null
                ? null
                : await episodeRepository.GetEpisode(command.PodcastId, command.EpisodeId);
            if (podcast is null || episode is null)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.NotFound, Error: "Podcast or episode not found");
            }

            if (episode.Removed)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.BadRequest,
                    Error: "A removed episode cannot be attached");
            }

            if (episode.IsHeldForApproval())
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.BadRequest,
                    Error: "An episode held for approval cannot be attached");
            }

            if (command.Request.TargetId == episode.Id)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.BadRequest,
                    Error: "The episode cannot be attached to itself");
            }

            await episodeServiceMaterialiser.MaterialiseAsync(podcast, episode, cancellationToken);
            var source = ServiceLinkMap.CloneWithoutNulls(episode.Services) ?? new Dictionary<string, ServiceLink>();
            var youTubeId = episode.Ids?.YouTube ??
                            (source.TryGetValue(ServiceKeys.YouTube, out var yt) && yt.Url is not null
                                ? YouTubeIdResolver.Extract(yt.Url)
                                : null);
            if (string.IsNullOrWhiteSpace(youTubeId))
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.BadRequest,
                    Error: "The episode has no YouTube upload to attach");
            }

            return command.Request.TargetKind switch
            {
                ContentKind.NewsReport => await AttachToNews(
                    podcast, episode, source, command.Request.TargetId, cancellationToken),
                ContentKind.TvShowEpisode => await AttachToTv(
                    podcast, episode, source, command.Request.TargetId, cancellationToken),
                ContentKind.Episode => await AttachToEpisode(
                    podcast, episode, source, command.Request.TargetId, cancellationToken),
                _ => new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.BadRequest, Error: "Unknown targetKind")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed to attach episode '{episodeId}' of podcast '{podcastId}' as a YouTube alternate.",
                nameof(AttachAsync), command.EpisodeId, command.PodcastId);
            return new EpisodeYouTubeAlternateAttachResult(EpisodeYouTubeAlternateAttachStatus.Failed);
        }
    }

    private async Task<EpisodeYouTubeAlternateAttachResult> AttachToFilm(
        EpisodeYouTubeAlternateAttachCommand command,
        CancellationToken cancellationToken)
    {
        var moved = await episodeFilmMoveService.MoveAsync(
            new EpisodeFilmMoveCommand(
                command.PodcastId,
                command.EpisodeId,
                new EpisodeFilmMoveRequest { FilmId = command.Request.TargetId }),
            cancellationToken);
        return moved.Status switch
        {
            EpisodeFilmMoveStatus.Accepted => new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Accepted, moved.FilmId, moved.Name),
            EpisodeFilmMoveStatus.NotFound => new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.NotFound, moved.FilmId, moved.Name, moved.Error),
            EpisodeFilmMoveStatus.BadRequest => new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.BadRequest, moved.FilmId, moved.Name, moved.Error),
            EpisodeFilmMoveStatus.Conflict => new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Conflict, moved.FilmId, moved.Name, moved.Error),
            _ => new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Failed, moved.FilmId, moved.Name, moved.Error,
                moved.FailureIndexing)
        };
    }

    private async Task<EpisodeYouTubeAlternateAttachResult> AttachToNews(
        Podcast podcast,
        Episode episode,
        Dictionary<string, ServiceLink> source,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        NewsReport? report = null;
        for (var attempt = 1; attempt <= MaxSaveAttempts && report is null; attempt++)
        {
            var found = await newsReportRepository.GetBy(item => item.Id == targetId);
            if (found is null)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.NotFound, Error: "News report not found");
            }

            var version = await newsReportRepository.GetNewsReportWithETag(found.NewsOrganisationId, found.Id);
            if (version is null)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.NotFound, Error: "News report not found");
            }

            var latest = version.Item;
            if (latest.IsRemoved())
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.BadRequest, latest.Id, latest.Title,
                    "A removed news report cannot take an episode's links");
            }

            var held = await YouTubeHeldByAnother(latest, source, episode.Id, latest.Title, cancellationToken);
            if (held is not null)
            {
                return held;
            }

            var applied = ApplyMerge(latest, latest.Title, source, episode, podcast, out var conflict);
            if (conflict is not null)
            {
                return conflict;
            }

            if (applied is null)
            {
                report = latest;
                break;
            }

            if (await newsReportRepository.TryReplace(latest, version.ETag))
            {
                report = latest;
                break;
            }
        }

        if (report is null)
        {
            return new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Failed, targetId,
                Error: "The news report kept changing; send again");
        }

        return await Finish(
            podcast, episode, source, report, report.Title,
            [CataloguePlayableSearchDocument.For(report)],
            CatalogueShortId.PlayablePath(report.Title?.Trim() ?? "News", report.Id, CatalogueShortId.NewsReport),
            cancellationToken,
            current => ServiceLinkMerge.Plan(current.Services, source, current.AdditionalYouTube),
            () => newsReportRepository.GetBy(item => item.Id == report.Id));
    }

    private async Task<EpisodeYouTubeAlternateAttachResult> AttachToTv(
        Podcast podcast,
        Episode episode,
        Dictionary<string, ServiceLink> source,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        TvShowEpisode? latest = null;
        for (var attempt = 1; attempt <= MaxSaveAttempts && latest is null; attempt++)
        {
            var found = await tvShowEpisodeRepository.GetBy(item => item.Id == targetId);
            if (found is null)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.NotFound, Error: "TV episode not found");
            }

            var version = await tvShowEpisodeRepository.GetTvShowEpisodeWithETag(found.TvShowId, found.Id);
            if (version is null)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.NotFound, Error: "TV episode not found");
            }

            var current = version.Item;
            if (current.IsRemoved())
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.BadRequest, current.Id, current.Title,
                    "A removed TV episode cannot take an episode's links");
            }

            var held = await YouTubeHeldByAnother(current, source, episode.Id, current.Title, cancellationToken);
            if (held is not null)
            {
                return held;
            }

            var applied = ApplyMerge(current, current.Title, source, episode, podcast, out var conflict);
            if (conflict is not null)
            {
                return conflict;
            }

            if (applied is null)
            {
                latest = current;
                break;
            }

            if (await tvShowEpisodeRepository.TryReplace(current, version.ETag))
            {
                latest = current;
                break;
            }
        }

        if (latest is null)
        {
            return new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Failed, targetId,
                Error: "The TV episode kept changing; send again");
        }

        return await Finish(
            podcast, episode, source, latest, latest.Title,
            [CataloguePlayableSearchDocument.For(latest)],
            CatalogueShortId.PlayablePath(latest.Title?.Trim() ?? "Episode", latest.Id, CatalogueShortId.TvShowEpisode),
            cancellationToken,
            current => ServiceLinkMerge.Plan(current.Services, source, current.AdditionalYouTube),
            () => tvShowEpisodeRepository.GetBy(item => item.Id == latest.Id));
    }

    private async Task<EpisodeYouTubeAlternateAttachResult> AttachToEpisode(
        Podcast sourcePodcast,
        Episode sourceEpisode,
        Dictionary<string, ServiceLink> source,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        Episode? latest = null;
        for (var attempt = 1; attempt <= MaxSaveAttempts && latest is null; attempt++)
        {
            var found = await episodeRepository.GetBy(item => item.Id == targetId);
            if (found is null)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.NotFound, Error: "Episode not found");
            }

            var version = await episodeRepository.GetEpisodeWithETag(found.PodcastId, found.Id);
            if (version is null)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.NotFound, Error: "Episode not found");
            }

            var current = version.Item;
            if (current.Removed)
            {
                return new EpisodeYouTubeAlternateAttachResult(
                    EpisodeYouTubeAlternateAttachStatus.BadRequest, current.Id, current.Title,
                    "A removed episode cannot take another episode's links");
            }

            var held = await YouTubeHeldByAnother(current, source, sourceEpisode.Id, current.Title, cancellationToken);
            if (held is not null)
            {
                return held;
            }

            var applied = ApplyMerge(current, current.Title, source, sourceEpisode, sourcePodcast, out var conflict);
            if (conflict is not null)
            {
                return conflict;
            }

            if (applied is null)
            {
                latest = current;
                break;
            }

            if (await episodeRepository.TryReplace(current, version.ETag))
            {
                latest = current;
                break;
            }
        }

        if (latest is null)
        {
            return new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Failed, targetId,
                Error: "The episode kept changing; send again");
        }

        var targetPodcast = await podcastRepository.GetPodcast(latest.PodcastId);
        if (targetPodcast is null)
        {
            return new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Failed, latest.Id, latest.Title,
                "The target episode's podcast was not found after the save");
        }

        return await Finish(
            sourcePodcast, sourceEpisode, source, latest, latest.Title,
            [new PodcastEpisode(targetPodcast, latest).ToEpisodeSearchRecord()],
            CatalogueShortId.PlayablePath(latest.Title?.Trim() ?? "Episode", latest.Id, nameof(ContentKind.Episode)),
            cancellationToken,
            current => ServiceLinkMerge.Plan(current.Services, source, current.AdditionalYouTube),
            () => episodeRepository.GetBy(item => item.Id == latest.Id));
    }

    private async Task<EpisodeYouTubeAlternateAttachResult?> YouTubeHeldByAnother(
        RedditPodcastPoster.Models.Catalogue.IPlayable target,
        Dictionary<string, ServiceLink> source,
        Guid sourceEpisodeId,
        string? name,
        CancellationToken cancellationToken)
    {
        var except = new List<Guid> { sourceEpisodeId };
        if (TargetIdFrom(target) is Guid targetId)
        {
            except.Add(targetId);
        }

        var message = await YouTubeAlternateOwnership.ConflictMessageIfHeld(
            urlMembershipLookup,
            YouTubeAlternateOwnership.IdsAMergeWouldAdd(target, source),
            except,
            cancellationToken);
        if (message is null)
        {
            return null;
        }

        return new EpisodeYouTubeAlternateAttachResult(
            EpisodeYouTubeAlternateAttachStatus.Conflict, TargetIdFrom(target), name, message);
    }

    /// <summary>
    /// Applies the planned service-link merge and YouTube alternates onto <paramref name="target"/>.
    /// Returns the target when a write is needed, or null when there is nothing to save. A conflict result is
    /// returned through <paramref name="conflict"/>.
    /// </summary>
    private static RedditPodcastPoster.Models.Catalogue.IPlayable? ApplyMerge(
        RedditPodcastPoster.Models.Catalogue.IPlayable target,
        string? name,
        Dictionary<string, ServiceLink> source,
        Episode episode,
        Podcast podcast,
        out EpisodeYouTubeAlternateAttachResult? conflict)
    {
        conflict = null;
        var plan = ServiceLinkMerge.Plan(target.Services, source, target.AdditionalYouTube);
        if (plan.HasConflicts)
        {
            var item = plan.Conflicts[0];
            conflict = new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Conflict, TargetIdFrom(target), name,
                $"'{name}' already has a different {item.Service} link ({item.TargetUrl}). " +
                "This item holds one link for this service.");
            return null;
        }

        var addedSubjects = episode.Subjects
            .Where(subject => !target.Subjects.Contains(subject, StringComparer.OrdinalIgnoreCase) &&
                              !target.RemovedSubjects.Contains(subject, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (!plan.HasWork && addedSubjects.Count == 0)
        {
            return null;
        }

        target.Services = ServiceLinkMerge.Apply(target.Services, plan);
        target.AdditionalYouTube = ServiceLinkMerge.ApplyYouTubeAlternates(
            target.AdditionalYouTube,
            plan,
            DateTime.UtcNow,
            episode,
            string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) ? null : podcast.YouTubeChannelId,
            podcast.Name);
        if (target is RedditPodcastPoster.Models.Catalogue.ICatalogueYouTubeItem catalogue)
        {
            EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(catalogue);
        }

        target.Subjects.AddRange(addedSubjects);
        return target;
    }

    private async Task<EpisodeYouTubeAlternateAttachResult> Finish<TTarget>(
        Podcast podcast,
        Episode episode,
        Dictionary<string, ServiceLink> source,
        TTarget target,
        string? name,
        IReadOnlyList<EpisodeSearchRecord> searchDocuments,
        string shortLinkPath,
        CancellationToken cancellationToken,
        Func<TTarget, ServiceLinkMergePlan> planOn,
        Func<Task<TTarget?>> reload)
        where TTarget : RedditPodcastPoster.Models.Catalogue.IPlayable
    {
        var targetId = TargetIdFrom(target);
        if (!await searchSwap.UploadAsync(searchDocuments, cancellationToken))
        {
            logger.LogError(
                "{method}: Search push failed for '{targetId}'. Episode row kept; re-send to resume.",
                nameof(Finish), targetId);
            return new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Failed, targetId, name, FailureIndexing: true);
        }

        if (!await searchSwap.DeleteAsync([episode.Id], cancellationToken))
        {
            logger.LogError(
                "{method}: Search delete failed for episode '{episodeId}'. Episode row kept; re-send to resume.",
                nameof(Finish), episode.Id);
            return new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Failed, targetId, name, FailureIndexing: true);
        }

        var current = await reload();
        if (current is null || planOn(current) is { HasWork: true } or { HasConflicts: true })
        {
            logger.LogError(
                "{method}: Target '{targetId}' was deleted or lost episode '{episodeId}' links after the save. Episode row kept.",
                nameof(Finish), targetId, episode.Id);
            await RestoreEpisodeSearchDocument(podcast, episode, cancellationToken);
            return new EpisodeYouTubeAlternateAttachResult(
                EpisodeYouTubeAlternateAttachStatus.Failed, targetId, name, FailureIndexing: true);
        }

        await episodeRepository.Delete(podcast.Id, episode.Id);
        logger.LogInformation(
            "Attached episode '{episodeId}' of podcast '{podcastId}' as a YouTube alternate on '{targetId}'.",
            episode.Id, podcast.Id, targetId);
        await RedirectShortLink(podcast, episode, shortLinkPath, targetId);
        return new EpisodeYouTubeAlternateAttachResult(
            EpisodeYouTubeAlternateAttachStatus.Accepted, targetId, name);
    }

    private static Guid? TargetIdFrom(RedditPodcastPoster.Models.Catalogue.IPlayable target) =>
        target switch
        {
            NewsReport report => report.Id,
            TvShowEpisode episode => episode.Id,
            Episode episode => episode.Id,
            _ => null
        };

    private async Task RestoreEpisodeSearchDocument(Podcast podcast, Episode episode, CancellationToken cancellationToken)
    {
        try
        {
            if (!await searchSwap.UploadAsync(
                    [new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord()], cancellationToken))
            {
                logger.LogError("{method}: Search re-upload failed for episode '{episodeId}'.",
                    nameof(RestoreEpisodeSearchDocument), episode.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Search re-upload failed for episode '{episodeId}'.",
                nameof(RestoreEpisodeSearchDocument), episode.Id);
        }
    }

    private async Task RedirectShortLink(Podcast podcast, Episode episode, string path, Guid? targetId)
    {
        try
        {
            var result = await shortnerService.Redirect(new PodcastEpisode(podcast, episode), path);
            if (!result.Success)
            {
                logger.LogWarning(
                    "{method}: Short link for episode '{episodeId}' was not pointed at '{targetId}'.",
                    nameof(RedirectShortLink), episode.Id, targetId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Short-link redirect failed for episode '{episodeId}'.",
                nameof(RedirectShortLink), episode.Id);
        }
    }
}
