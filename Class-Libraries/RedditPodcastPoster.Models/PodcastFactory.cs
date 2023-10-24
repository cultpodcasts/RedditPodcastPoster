namespace RedditPodcastPoster.Models;

public class PodcastFactory
{
    public Podcast Create(string podcastName)
    {
        var fileKey = FileKeyFactory.GetFileKey(podcastName);
        return new Podcast(Guid.NewGuid()) {Name = podcastName, FileKey = fileKey};
    }
}