namespace RedditPodcastPoster.Discovery.Models;

public class PodcastResult(
    Guid podcastId,
    bool indexAllEpisodes,
    bool removed = false,
    string youTubePlaylistId = "",
    bool suppressesUnrelatedShows = false)
{
    public Guid PodcastId { get; } = podcastId;
    public bool IndexAllEpisodes { get; } = indexAllEpisodes;
    public bool Removed { get; } = removed;

    /// <summary>Empty for a channel-wide podcast or a catalogue parent. Set for a playlist-scoped show.</summary>
    public string YouTubePlaylistId { get; } = youTubePlaylistId ?? string.Empty;

    /// <summary>
    /// A removed catalogue parent still covers the channel, including playlist-scoped shows that share it.
    /// </summary>
    public bool SuppressesUnrelatedShows { get; } = suppressesUnrelatedShows;
}