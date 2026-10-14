using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// <see cref="IYouTubeSearchResultFinder"/> decorator: runs <see cref="YouTubeSearchResultFinder"/>, then
/// prefers a non-📱 / moving contender for the same episode via <see cref="YouTubeCandidatePreferenceApplier"/>.
/// </summary>
public class PreferringYouTubeSearchResultFinder(
    YouTubeSearchResultFinder inner,
    YouTubeCandidatePreferenceApplier applier) : IYouTubeSearchResultFinder
{
    public async Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(
        EpisodeModel episode,
        IList<SearchResult> searchResults,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext,
        Podcast? podcast = null)
    {
        var response = await inner.FindMatchingYouTubeVideo(
            episode, searchResults, youTubePublishDelay, indexingContext, podcast);
        var videoResults = searchResults.Where(x => !string.IsNullOrEmpty(x.Id?.VideoId)).ToList();
        return await applier.ApplyAsync(
            episode,
            videoResults,
            response,
            response?.SearchResult,
            YouTubeEnrichmentCandidate.ScoringPodcast(podcast, youTubePublishDelay),
            indexingContext,
            x => x.Id.VideoId,
            x => x.Snippet?.Title ?? string.Empty,
            YouTubeEnrichmentCandidate.ToEpisode,
            (item, video) => new FindEpisodeResponse(item, Video: video));
    }
}