using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public class CategorisedItemProcessor(
    IPodcastProcessor podcastProcessor,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IPodcastAndEpisodeFactory podcastAndEpisodeFactory,
    IHeroEpisodePromoter heroEpisodePromoter,
    ILogger<CategorisedItem> logger) : ICategorisedItemProcessor
{
    public async Task<SubmitResult> ProcessCategorisedItem(CategorisedItem categorisedItem, SubmitOptions submitOptions)
    {
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
            submitResult = await podcastProcessor.AddEpisodeToExistingPodcast(categorisedItem);

            if (submitOptions.PersistToDatabase)
            {
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
            var result = await podcastAndEpisodeFactory.CreatePodcastWithEpisode(
                categorisedItem,
                submitOptions.PodcastName);
            submitResult = new SubmitResult(SubmitResultState.Created,
                SubmitResultState.Created,
                result.SubmitEpisodeDetails,
                result.NewEpisode,
                result.NewPodcast);
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
                release: submitResult.Episode?.Release,
                episodeResult: submitResult.EpisodeResult.ToString());
        }

        LogSubmitEpisodeState(submitResult);
        return submitResult;
    }

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
                release: episode.Release,
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