using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

public class PodcastProcessor(
    IEpisodeHelper episodeHelper,
    IEpisodeEnricher episodeEnricher,
    IEpisodeFactory episodeFactory,
    ISubjectEnricher subjectEnricher,
    ILogger<PodcastProcessor> logger) : IPodcastProcessor
{
    public async Task<SubmitResult> AddEpisodeToExistingPodcast(
        CategorisedItem categorisedItem)
    {
        var matchingEpisodes = categorisedItem.MatchingEpisode != null
            ? [categorisedItem.MatchingEpisode]
            : categorisedItem.MatchingPodcastEpisodes!.Where(episode =>
                episodeHelper.IsMatchingEpisode(episode, categorisedItem)).ToArray();

        Episode? matchingEpisode;
        if (matchingEpisodes!.Count() > 1)
        {
            var title = categorisedItem.ResolvedAppleItem?.EpisodeTitle ??
                        categorisedItem.ResolvedSpotifyItem?.EpisodeTitle ??
                        categorisedItem.ResolvedYouTubeItem?.EpisodeTitle;
            matchingEpisode = FuzzyMatcher.Match(title!, matchingEpisodes, x => x.Title);
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
        Episode? episode = null;
        if (matchingEpisode == null)
        {
            episodeResult = SubmitResultState.Created;
            episode = episodeFactory.CreateEpisode(categorisedItem);
            episode.SetPodcastProperties(categorisedItem.MatchingPodcast);
            var subjectsResult = await subjectEnricher.EnrichSubjects(
                episode,
                new SubjectEnrichmentOptions(
                    categorisedItem.MatchingPodcast.IgnoredAssociatedSubjects,
                    categorisedItem.MatchingPodcast.IgnoredSubjects,
                    categorisedItem.MatchingPodcast.DefaultSubject,
                    categorisedItem.MatchingPodcast.DescriptionRegex));

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

            submitEpisodeDetails = new SubmitEpisodeDetails(
                episode.Urls.Spotify != null,
                episode.Urls.Apple != null,
                episode.Urls.YouTube != null,
                subjectsResult.Additions,
                episode.Urls.BBC != null,
                episode.Urls.InternetArchive != null);
        }
        else
        {
            episodeResult = appliedEpisodeResult;
            episode = matchingEpisode;
            if (!episode.Subjects.Any())
            {
                var subjectsResult = await subjectEnricher.EnrichSubjects(
                    episode,
                    new SubjectEnrichmentOptions(
                        categorisedItem.MatchingPodcast.IgnoredAssociatedSubjects,
                        categorisedItem.MatchingPodcast.IgnoredSubjects,
                        categorisedItem.MatchingPodcast.DefaultSubject,
                        categorisedItem.MatchingPodcast.DescriptionRegex));
                if (episode.Subjects.Any())
                {
                    episodeResult = SubmitResultState.Enriched;
                }
            }
        }

        return new SubmitResult(episodeResult, podcastResult, submitEpisodeDetails, episode);
    }
}