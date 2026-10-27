using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

/// <summary>
/// Hero auto-promote for a playable a URL submit has just stored under a news organisation or TV show. Same rule as
/// <c>CategorisedItemProcessor</c> uses for a podcast (via <see cref="HeroAutoPromoteSelector"/>): the parent is flagged
/// always-promote-as-hero, the item is not ignored or removed, and it was released inside the week window measured
/// from <paramref name="timeProvider"/>. Shared by <see cref="CatalogueParentSubmitter"/> and
/// <see cref="CatalogueKindSubmitter"/> so both submit paths promote the same way.
/// </summary>
internal static class CatalogueHeroAutoPromotion
{
    public static async Task PromoteIfEligible(
        ILogger logger,
        IHeroEpisodePromoter heroEpisodePromoter,
        TimeProvider timeProvider,
        Podcast transient,
        Episode episode,
        Guid playableId,
        EpisodeCreationSource creationSource)
    {
        if (transient.AlwaysPromoteAsHero != true)
        {
            return;
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var skipReason = HeroAutoPromoteSelector.GetSkipReason(transient, episode, utcNow);
        if (skipReason != HeroAutoPromoteSkipReason.None)
        {
            HeroAutoPromoteLogger.LogSkipped(
                logger,
                skipReason,
                playableId,
                transient.Id,
                transient.AlwaysPromoteAsHero,
                release: episode.ReleaseUtc,
                cutoff: skipReason == HeroAutoPromoteSkipReason.OutsideWeekWindow
                    ? HeroAutoPromoteSelector.GetCutoff(utcNow)
                    : null,
                episodeResult: SubmitResultState.Created.ToString());
            return;
        }

        HeroAutoPromoteLogger.LogAttempt(logger, creationSource, transient.Id, [playableId]);
        await heroEpisodePromoter.PromoteAsync([playableId]);
    }
}
