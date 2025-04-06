using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
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
        Episode[] matchingEpisodes;
        if (categorisedItem.MatchingEpisode != null)
        {
            matchingEpisodes = [categorisedItem.MatchingEpisode];
        }
        else
        {
            IEnumerable<Episode> candidateEpisodes = categorisedItem.MatchingPodcast!.Episodes;

            candidateEpisodes = categorisedItem.Authority switch
            {
                Service.YouTube => candidateEpisodes.Where(episode =>
                    episode.Urls.YouTube == null ||
                    (categorisedItem.ResolvedYouTubeItem != null &&
                     !string.IsNullOrEmpty(categorisedItem.ResolvedYouTubeItem.EpisodeId) &&
                     categorisedItem.ResolvedYouTubeItem.EpisodeId != episode.YouTubeId)
                ),
                Service.Apple => candidateEpisodes.Where(episode =>
                    episode.Urls.Apple == null ||
                    (categorisedItem.ResolvedAppleItem is {EpisodeId: not null} &&
                     categorisedItem.ResolvedAppleItem.EpisodeId != episode.AppleId)
                ),
                Service.Spotify => candidateEpisodes.Where(episode =>
                    episode.Urls.Spotify == null ||
                    (categorisedItem.ResolvedSpotifyItem != null &&
                     !string.IsNullOrEmpty(categorisedItem.ResolvedSpotifyItem.EpisodeId) &&
                     categorisedItem.ResolvedSpotifyItem.EpisodeId != episode.SpotifyId)
                ),
                _ => candidateEpisodes
            };

            matchingEpisodes = candidateEpisodes
                .Where(episode => episodeHelper.IsMatchingEpisode(episode, categorisedItem)).ToArray();
        }

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
            $"Modifying podcast with name '{categorisedItem.MatchingPodcast!.Name}' and id '{categorisedItem.MatchingPodcast.Id}'.");

        var (podcastResult, appliedEpisodeResult, submitEpisodeDetails) =
            episodeEnricher.ApplyResolvedPodcastServiceProperties(
                categorisedItem.MatchingPodcast,
                categorisedItem,
                matchingEpisode);

        Guid episodeId;
        SubmitResultState episodeResult;
        if (matchingEpisode == null)
        {
            episodeResult = SubmitResultState.Created;
            var episode = episodeFactory.CreateEpisode(categorisedItem);
            var subjectsResult = await subjectEnricher.EnrichSubjects(
                episode,
                new SubjectEnrichmentOptions(
                    categorisedItem.MatchingPodcast.IgnoredAssociatedSubjects,
                    categorisedItem.MatchingPodcast.IgnoredSubjects,
                    categorisedItem.MatchingPodcast.DefaultSubject,
                    categorisedItem.MatchingPodcast.DescriptionRegex));
            categorisedItem.MatchingPodcast.Episodes.Add(episode);
            categorisedItem.MatchingPodcast.Episodes =
                categorisedItem.MatchingPodcast.Episodes.OrderByDescending(x => x.Release).ToList();
            episodeId = episode.Id;
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
            episodeId = matchingEpisode.Id;
        }

        return new SubmitResult(episodeResult, podcastResult, submitEpisodeDetails, episodeId);
    }
}