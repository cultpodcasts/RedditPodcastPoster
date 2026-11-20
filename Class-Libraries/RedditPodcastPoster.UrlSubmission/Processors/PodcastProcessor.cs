using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public class PodcastProcessor(
    IEpisodeHelper episodeHelper,
    IEpisodeEnricher episodeEnricher,
    IEpisodeFactory episodeFactory,
    ISubjectEnricher subjectEnricher,
    ISubjectEnrichmentOptionsFactory subjectEnrichmentOptionsFactory,
    IEpisodeGuestEnricher guestEnricher,
    ILogger<PodcastProcessor> logger) : IPodcastProcessor
{
    public async Task<SubmitResult> AddEpisodeToExistingPodcast(CategorisedItem categorisedItem)
    {
        var matchingEpisodes = categorisedItem.MatchingEpisode != null
            ? [categorisedItem.MatchingEpisode]
            : categorisedItem.MatchingPodcastEpisodes!.Where(episode =>
                // #1484: a split-off held for curator approval (AwaitingApproval or Rejected) is matched only by its own
                // service id. By title alone (after a partly saved overwrite) it would be enriched in place of the
                // stored episode; by id it is that episode, and matching it (still hidden) avoids a duplicate public
                // episode.
                SplitOffMatching.MayMatch(episode, categorisedItem) &&
                episodeHelper.IsMatchingEpisode(episode, categorisedItem)).ToArray();

        // #1484: of several matches, the episode holding the submitted (authority) service's id is the one submitted,
        // e.g. a split-off holding the submitted Spotify id over its stored episode matching by YouTube id and title.
        if (matchingEpisodes.Length > 1)
        {
            var holdingAuthorityId = matchingEpisodes
                .Where(episode => SplitOffMatching.HoldsAuthorityId(episode, categorisedItem))
                .ToArray();
            if (holdingAuthorityId.Length > 0)
            {
                matchingEpisodes = holdingAuthorityId;
            }
        }

        Episode? matchingEpisode;
        if (matchingEpisodes.Count() > 1)
        {
            var title = categorisedItem.ResolvedAppleItem?.EpisodeTitle ??
                        categorisedItem.ResolvedSpotifyItem?.EpisodeTitle ??
                        categorisedItem.ResolvedYouTubeItem?.EpisodeTitle ??
                        categorisedItem.ResolvedNonPodcastServiceItem?.Title;
            matchingEpisode = string.IsNullOrWhiteSpace(title)
                ? null
                : FuzzyMatcher.Match(title, matchingEpisodes, x => x.Title);
        }
        else
        {
            matchingEpisode = matchingEpisodes.SingleOrDefault();
        }

        logger.LogInformation(
            "Modifying podcast with name '{matchingPodcastName}' and id '{matchingPodcastId}'.",
            categorisedItem.MatchingPodcast!.Name, categorisedItem.MatchingPodcast.Id);

        var (podcastResult, appliedEpisodeResult, submitEpisodeDetails) =
            episodeEnricher.ApplyResolvedPodcastServiceProperties(
                categorisedItem.MatchingPodcast,
                categorisedItem,
                matchingEpisode);

        SubmitResultState episodeResult;
        Episode? episode;
        string[]? subjectAdditions = null;
        PersonMatch[]? peopleAdditions = null;
        PersonMatch[]? guestSuggestions = null;
        if (matchingEpisode == null)
        {
            episodeResult = SubmitResultState.Created;
            episode = episodeFactory.CreateEpisode(categorisedItem);
            episode.SetPodcastProperties(categorisedItem.MatchingPodcast, inheritLanguageIfUnset: true);
            var subjectsResult = await subjectEnricher.EnrichSubjects(
                episode,
                await subjectEnrichmentOptionsFactory.CreateAsync(
                    categorisedItem.MatchingPodcast,
                    episode));
            var guestsResult = await guestEnricher.EnrichGuests(episode);

            if (!episode.Subjects.Any())
            {
                logger.LogError(
                    "{method}: No subjects applied to new episode '{episodeTitle}' ({episodeId}) for podcast '{podcastName}' ({podcastId}). Configured default-subject: '{defaultSubject}'.",
                    nameof(AddEpisodeToExistingPodcast), episode.Title, episode.Id,
                    categorisedItem.MatchingPodcast.Name, categorisedItem.MatchingPodcast.Id,
                    categorisedItem.MatchingPodcast.DefaultSubject ?? "(none)");
            }
            else if (!string.IsNullOrWhiteSpace(categorisedItem.MatchingPodcast.DefaultSubject) &&
                     episode.Subjects.Count == 1 &&
                     string.Equals(episode.Subjects[0], categorisedItem.MatchingPodcast.DefaultSubject,
                         StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "{method}: No specific subjects matched for new episode '{episodeTitle}' ({episodeId}) for podcast '{podcastName}' ({podcastId}); applied default-subject '{defaultSubject}'.",
                    nameof(AddEpisodeToExistingPodcast), episode.Title, episode.Id,
                    categorisedItem.MatchingPodcast.Name, categorisedItem.MatchingPodcast.Id,
                    categorisedItem.MatchingPodcast.DefaultSubject);
            }

            submitEpisodeDetails = SubmitEpisodeDetails.FromEpisode(
                episode,
                subjectsResult.Additions,
                guestsResult.Additions,
                guestsResult.SkippedLowConfidence);
        }
        else
        {
            episodeResult = appliedEpisodeResult;
            episode = matchingEpisode;
            if (!episode.Subjects.Any())
            {
                var subjectsResult = await subjectEnricher.EnrichSubjects(
                    episode,
                    await subjectEnrichmentOptionsFactory.CreateAsync(
                        categorisedItem.MatchingPodcast,
                        episode));
                if (subjectsResult.Additions.Length > 0)
                {
                    subjectAdditions = subjectsResult.Additions;
                }

                if (episode.Subjects.Any())
                {
                    episodeResult = SubmitResultState.Enriched;
                }
            }

            if (episode.Guests is not { Length: > 0 })
            {
                var guestsResult = await guestEnricher.EnrichGuests(episode);
                if (guestsResult.Additions.Length > 0)
                {
                    peopleAdditions = guestsResult.Additions;
                    episodeResult = SubmitResultState.Enriched;
                }

                if (guestsResult.SkippedLowConfidence.Length > 0)
                {
                    guestSuggestions = guestsResult.SkippedLowConfidence;
                }
            }

            if (subjectAdditions != null || peopleAdditions != null || guestSuggestions != null)
            {
                submitEpisodeDetails = submitEpisodeDetails! with
                {
                    Subjects = subjectAdditions ?? submitEpisodeDetails!.Subjects,
                    People = peopleAdditions,
                    GuestSuggestions = guestSuggestions
                };
            }
        }

        return new SubmitResult(episodeResult, podcastResult, submitEpisodeDetails, episode,
            categorisedItem.MatchingPodcast);
    }
}