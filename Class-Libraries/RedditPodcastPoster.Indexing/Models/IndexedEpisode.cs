using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Indexing.Models;

public record IndexedEpisode(Episode Episode, bool Spotify, bool Apple, bool YouTube)
{
    public string[] Subjects { get; set; } = [];
}