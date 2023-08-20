using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public class FilenameSelector : IFilenameSelector
{
    public string GetKey(Podcast podcast)
    {
        return podcast.FileKey;
    }
}