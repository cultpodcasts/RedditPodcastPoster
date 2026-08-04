using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

/// <summary>
/// Adapts YouTube search/playlist candidates to <see cref="EpisodeModel"/> and applies
/// <see cref="CrossPlatformMatchScorer"/> for enrichment acceptance (delay is a score signal).
/// </summary>
public static class YouTubeEnrichmentCandidate
{
    public static EpisodeModel ToEpisode(
        string title,
        string? description,
        DateTime release,
        TimeSpan length,
        string youTubeId) =>
        new()
        {
            Title = title,
            Description = description ?? string.Empty,
            Release = release,
            Length = length,
            YouTubeId = youTubeId
        };

    public static EpisodeModel ToEpisode(SearchResult searchResult, Google.Apis.YouTube.v3.Data.Video? video)
    {
        var length = video?.GetLength() ?? TimeSpan.Zero;
        var description = searchResult.Snippet?.Description
                          ?? video?.Snippet?.Description
                          ?? string.Empty;
        return ToEpisode(
            searchResult.Snippet.Title,
            description,
            searchResult.Snippet.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.MinValue,
            length,
            searchResult.Id.VideoId);
    }

    public static EpisodeModel ToEpisode(PlaylistItem playlistItem, Google.Apis.YouTube.v3.Data.Video? video)
    {
        var length = video?.GetLength() ?? TimeSpan.Zero;
        var description = playlistItem.Snippet?.Description
                          ?? video?.Snippet?.Description
                          ?? string.Empty;
        return ToEpisode(
            playlistItem.Snippet.Title,
            description,
            playlistItem.Snippet.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.MinValue,
            length,
            playlistItem.GetVideoId());
    }

    public static Podcast ScoringPodcast(Podcast? podcast, TimeSpan? youTubePublishDelay)
    {
        if (podcast != null)
        {
            return podcast;
        }

        return new Podcast
        {
            YouTubePublicationOffset = youTubePublishDelay?.Ticks
        };
    }

    public static bool MeetsStrongMatch(
        EpisodeModel existingEpisode,
        EpisodeModel youTubeCandidate,
        Podcast podcast) =>
        CrossPlatformMatchScorer.MeetsMatchThreshold(existingEpisode, youTubeCandidate, podcast);
}
