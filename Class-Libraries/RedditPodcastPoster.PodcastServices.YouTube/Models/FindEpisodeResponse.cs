using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record FindEpisodeResponse(SearchResult SearchResult, Google.Apis.YouTube.v3.Data.Video? Video = null);