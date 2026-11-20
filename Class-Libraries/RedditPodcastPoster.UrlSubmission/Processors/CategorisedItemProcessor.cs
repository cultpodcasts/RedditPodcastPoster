using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Languages;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public class CategorisedItemProcessor(
    IPodcastProcessor podcastProcessor,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IPodcastAndEpisodeFactory podcastAndEpisodeFactory,
    IHeroEpisodePromoter heroEpisodePromoter,
    ILogger<CategorisedItem> logger,
    ICatalogueLanguageGate? languageGate = null,
    ICatalogueKindSubmitter? catalogueKindSubmitter = null,
    IServiceLinkConflictGate? serviceLinkConflictGate = null) : ICategorisedItemProcessor
{
    public async Task<SubmitResult> ProcessCategorisedItem(CategorisedItem categorisedItem, SubmitOptions submitOptions)
    {
        if (submitOptions.LanguageChoice == CatalogueLanguageChoice.Cancel)
        {
            return LanguageHold(new CatalogueLanguageHold(string.Empty, string.Empty, []));
        }

        if (languageGate == null && (submitOptions.EnforceLanguageGate || submitOptions.LanguageProbe))
        {
            throw new InvalidOperationException("Language gate is not configured.");
        }

        if (catalogueKindSubmitter != null)
        {
            var kindResult = await catalogueKindSubmitter.TrySubmit(categorisedItem, submitOptions);
            if (kindResult != null)
            {
                return kindResult;
            }
        }

        void LogSubmitEpisodeState(SubmitResult submitResult)
        {
            if (submitResult.EpisodeResult is not (SubmitResultState.Created or SubmitResultState.Enriched))
            {
                return;
            }

            if (submitResult.Episode == null)
            {
                logger.LogError(
                    "ProcessCategorisedItem produced episode state '{EpisodeResult}' but no episode instance. Authority: '{Authority}', MatchingPodcastId: '{MatchingPodcastId}', PersistToDatabase: {PersistToDatabase}. Result: {SubmitResult}.",
                    submitResult.EpisodeResult,
                    categorisedItem.Authority,
                    categorisedItem.MatchingPodcast?.Id,
                    submitOptions.PersistToDatabase,
                    submitResult);
            }
            else
            {
                logger.LogInformation(
                    "ProcessCategorisedItem produced episode state '{EpisodeResult}' with episode id '{EpisodeId}'. Authority: '{Authority}', MatchingPodcastId: '{MatchingPodcastId}', PersistToDatabase: {PersistToDatabase}.",
                    submitResult.EpisodeResult,
                    submitResult.Episode.Id,
                    categorisedItem.Authority,
                    categorisedItem.MatchingPodcast?.Id,
                    submitOptions.PersistToDatabase);
            }
        }

        SubmitResult submitResult;
        if (categorisedItem.MatchingPodcast != null)
        {
            var admission = CatalogueLanguageAdmission.Proceed();
            if (submitOptions.EnforceLanguageGate)
            {
                admission = await Admit(categorisedItem, creatingNewParent: false, submitOptions);
                if (admission.Hold)
                {
                    return LanguageHold(admission.Prompt!);
                }

                if (submitOptions.LanguageProbe)
                {
                    return Probe(admission);
                }
            }
            else if (submitOptions.LanguageProbe)
            {
                return Probe(admission);
            }

            Episode? splitOffEpisode = null;
            var saveSplitOff = false;
            if (serviceLinkConflictGate != null)
            {
                var conflict = serviceLinkConflictGate.Evaluate(categorisedItem, submitOptions);
                switch (conflict.Kind)
                {
                    case ServiceLinkConflictOutcomeKind.Hold:
                        return new SubmitResult(
                            SubmitResultState.None,
                            SubmitResultState.None,
                            Podcast: categorisedItem.MatchingPodcast,
                            ServiceLinkConflict: conflict.Hold);
                    case ServiceLinkConflictOutcomeKind.Cancelled:
                        return new SubmitResult(SubmitResultState.None, SubmitResultState.None);
                    case ServiceLinkConflictOutcomeKind.Overwritten:
                        categorisedItem = categorisedItem with { MatchingEpisode = conflict.Episode };
                        splitOffEpisode = conflict.SplitOff;
                        saveSplitOff = conflict.SaveSplitOff;
                        break;
                }
            }

            submitResult = await podcastProcessor.AddEpisodeToExistingPodcast(categorisedItem);
            if (splitOffEpisode != null)
            {
                // The stored episode has lost its old links to the split-off, so it is saved even if the submitted
                // links happened to add nothing else.
                submitResult = submitResult with
                {
                    EpisodeResult = SubmitResultState.Enriched,
                    SplitOffEpisode = splitOffEpisode
                };
            }

            if (submitOptions.PersistToDatabase)
            {
                if (splitOffEpisode != null && saveSplitOff)
                {
                    // Saved first: if the stored episode's save then fails, its old links exist twice, never zero times.
                    // A reused split-off that gained no link is not saved again, so curator edits to it are kept (#1482).
                    await episodeRepository.Save(splitOffEpisode);
                }

                if (submitResult is { PodcastResult: SubmitResultState.Enriched })
                {
                    await podcastRepository.Save(categorisedItem.MatchingPodcast);
                }

                if (submitResult is
                    { Episode: not null, EpisodeResult: SubmitResultState.Created or SubmitResultState.Enriched })
                {
                    await episodeRepository.Save(submitResult.Episode);
                }
            }
            else
            {
                logger.LogWarning("Bypassing persisting podcast.");
            }
        }
        else
        {
            var admission = CatalogueLanguageAdmission.Proceed();
            if (submitOptions.EnforceLanguageGate)
            {
                admission = await Admit(categorisedItem, creatingNewParent: true, submitOptions);
                if (admission.Hold)
                {
                    return LanguageHold(admission.Prompt!);
                }

                if (submitOptions.LanguageProbe)
                {
                    return Probe(admission);
                }
            }
            else if (submitOptions.LanguageProbe)
            {
                return Probe(admission);
            }

            var result = await podcastAndEpisodeFactory.CreatePodcastWithEpisode(
                categorisedItem,
                submitOptions.PodcastName,
                admission.LanguageCode);
            submitResult = new SubmitResult(SubmitResultState.Created,
                SubmitResultState.Created,
                result.SubmitEpisodeDetails,
                result.NewEpisode,
                result.NewPodcast,
                DefaultLanguageFromDetection: admission.SetsDefaultLanguage);
            if (submitOptions.PersistToDatabase)
            {
                await podcastRepository.Save(result.NewPodcast);
                await episodeRepository.Save(result.NewEpisode);
            }
            else
            {
                logger.LogWarning("Bypassing persisting new-podcast.");
            }
        }

        var podcast = submitResult.Podcast ?? categorisedItem.MatchingPodcast;

        if (submitResult is { EpisodeResult: SubmitResultState.Created, Episode: not null })
        {
            var podcastId = submitResult.Episode.PodcastId != Guid.Empty
                ? submitResult.Episode.PodcastId
                : podcast?.Id ?? Guid.Empty;
            EpisodeCreationLogger.LogCreated(
                logger,
                submitResult.Episode,
                podcastId,
                submitOptions.CreationSource,
                categorisedItem.Authority,
                caller: "CategorisedItemProcessor.ProcessCategorisedItem");

            if (submitOptions.PersistToDatabase && podcast != null)
            {
                await PromoteCreatedEpisodeIfEligible(
                    podcast,
                    submitResult.Episode,
                    submitOptions.CreationSource);
            }
        }
        else if (podcast is { AlwaysPromoteAsHero: true })
        {
            HeroAutoPromoteLogger.LogSkipped(
                logger,
                HeroAutoPromoteSkipReason.NotCreated,
                submitResult.Episode?.Id ?? Guid.Empty,
                podcast.Id,
                podcast.AlwaysPromoteAsHero,
                release: submitResult.Episode?.ReleaseUtc,
                episodeResult: submitResult.EpisodeResult.ToString());
        }

        LogSubmitEpisodeState(submitResult);
        return submitResult;
    }

    private Task<CatalogueLanguageAdmission> Admit(
        CategorisedItem categorisedItem,
        bool creatingNewParent,
        SubmitOptions submitOptions) =>
        languageGate!.Evaluate(
            CatalogueLanguageTextReader.FromCategorisedItem(
                categorisedItem,
                submitOptions.PodcastName,
                creatingNewParent ? null : categorisedItem.MatchingPodcast),
            creatingNewParent,
            submitOptions);

    private static SubmitResult LanguageHold(CatalogueLanguageHold hold) =>
        new(SubmitResultState.None, SubmitResultState.None, LanguageHold: hold);

    private static SubmitResult Probe(CatalogueLanguageAdmission admission) =>
        new(
            SubmitResultState.None,
            SubmitResultState.None,
            DefaultLanguageFromDetection: admission.SetsDefaultLanguage);

    private async Task PromoteCreatedEpisodeIfEligible(
        Podcast podcast,
        Episode episode,
        EpisodeCreationSource creationSource)
    {
        var utcNow = DateTime.UtcNow;
        var skipReason = HeroAutoPromoteSelector.GetSkipReason(podcast, episode, utcNow);
        if (skipReason != HeroAutoPromoteSkipReason.None)
        {
            var cutoff = skipReason == HeroAutoPromoteSkipReason.OutsideWeekWindow
                ? HeroAutoPromoteSelector.GetCutoff(utcNow)
                : (DateTime?)null;
            HeroAutoPromoteLogger.LogSkipped(
                logger,
                skipReason,
                episode.Id,
                podcast.Id,
                podcast.AlwaysPromoteAsHero,
                release: episode.ReleaseUtc,
                cutoff: cutoff,
                episodeResult: SubmitResultState.Created.ToString());
            return;
        }

        HeroAutoPromoteLogger.LogAttempt(
            logger,
            creationSource,
            podcast.Id,
            [episode.Id]);
        await heroEpisodePromoter.PromoteAsync([episode.Id]);
    }
}