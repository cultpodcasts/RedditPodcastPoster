using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Indexing.Models;

public class IndexedEpisode(Episode episode, bool spotify, bool apple, bool youtube)
{
    public string[] Subjects { get; set; } = [];
    public Episode Episode { get; init; } = episode;
    public bool Spotify { get; init; } = spotify;
    public bool Apple { get; init; } = apple;
    public bool YouTube { get; init; } = youtube;
}