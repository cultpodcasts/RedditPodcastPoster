using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public record ChannelVideos(Channel Channel, IList<PlaylistItem> PlaylistItems);