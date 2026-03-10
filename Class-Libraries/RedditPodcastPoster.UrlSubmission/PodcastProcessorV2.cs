using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// V2 implementation that adds episodes to existing podcasts using detached IEpisodeRepository.
/// </summary>
public class PodcastProcessorV2(
    IEpisodeHelper episodeHelper,
    IEpisodeEnricher episodeEnricher,
    IEpisodeFactory episodeFactory,
    ISubjectEnricher subjectEnricher,
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<PodcastProcessorV2> logger) : IPodcastProcessorV2
{
    public async Task<SubmitResult> AddEpisodeToExistingPodcast(
        CategorisedItem categorisedItem)
    {
        if (categorisedItem.MatchingPodcast == null)
        {
            throw new ArgumentException("CategorisedItem must have a MatchingPodcast", nameof(categorisedItem));
        }

        // Load existing episodes from detached repository
        var existingEpisodes = await episodeRepository.GetByPodcastId(categorisedItem.MatchingPodcast.Id).ToListAsync();

        var matchingEpisodes = categorisedItem.MatchingEpisode != null
            ? [categorisedItem.MatchingEpisode]
            : existingEpisodes.Where(episode =>
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

        Guid episodeId;
        SubmitResultState episodeResult;
        if (matchingEpisode == null)
        {
            // Create new episode
            episodeResult = SubmitResultState.Created;
            var episode = episodeFactory.CreateEpisode(categorisedItem);
            var subjectsResult = await subjectEnricher.EnrichSubjects(
                episode,
                new SubjectEnrichmentOptions(
                    categorisedItem.MatchingPodcast.IgnoredAssociatedSubjects,
                    categorisedItem.MatchingPodcast.IgnoredSubjects,
                    categorisedItem.MatchingPodcast.DefaultSubject,
                    categorisedItem.MatchingPodcast.DescriptionRegex));

            // Save new episode to detached repository
            episode.PodcastId = categorisedItem.MatchingPodcast.Id;
            await episodeRepository.Save(episode);

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

            // If episode was updated, save changes to detached repository
            if (appliedEpisodeResult == SubmitResultState.Enriched)
            {
                var v2Episode = existingEpisodes.First(e => e.Id == matchingEpisode.Id);

                // Update V2 episode with changes from matching episode
                v2Episode.SpotifyId = matchingEpisode.SpotifyId;
                v2Episode.AppleId = matchingEpisode.AppleId;
                v2Episode.YouTubeId = matchingEpisode.YouTubeId;
                v2Episode.Urls = matchingEpisode.Urls;

                await episodeRepository.Save(v2Episode);
            }
        }

        // Save podcast metadata if updated
        if (podcastResult == SubmitResultState.Enriched)
        {
            await podcastRepository.Save(categorisedItem.MatchingPodcast);
        }

        return new SubmitResult(episodeResult, podcastResult, submitEpisodeDetails, matchingEpisode);
    }
}