using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public record FindEpisodeResponse(SearchResult SearchResult, Video? Video = null);