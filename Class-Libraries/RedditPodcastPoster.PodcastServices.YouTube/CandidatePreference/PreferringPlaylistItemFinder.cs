using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// <see cref="IPlaylistItemFinder"/> decorator: runs <see cref="PlaylistItemFinder"/>, then prefers a
/// non-📱 / moving contender for the same episode via <see cref="YouTubeCandidatePreferenceApplier"/>.
/// </summary>
public class PreferringPlaylistItemFinder(
    PlaylistItemFinder inner,
    YouTubeCandidatePreferenceApplier applier) : IPlaylistItemFinder
{
    public async Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(
        EpisodeModel episode,
        IList<PlaylistItem> searchResults,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext,
        Podcast? podcast = null)
    {
        var response = await inner.FindMatchingYouTubeVideo(
            episode, searchResults, youTubePublishDelay, indexingContext, podcast);
        return await applier.ApplyAsync(
            episode,
            searchResults,
            response,
            response?.PlaylistItem,
            YouTubeEnrichmentCandidate.ScoringPodcast(podcast, youTubePublishDelay),
            indexingContext,
            x => x.GetVideoId(),
            x => x.Snippet?.Title ?? string.Empty,
            YouTubeEnrichmentCandidate.ToEpisode,
            (item, video) => new FindEpisodeResponse(PlaylistItem: item, Video: video));
    }
}