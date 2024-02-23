namespace RedditPodcastPoster.Models;

public class PodcastFactory
{
    public Podcast Create(string podcastName)
    {
        podcastName = podcastName.Trim();
        var fileKey = FileKeyFactory.GetFileKey(podcastName);
        return new Podcast(Guid.NewGuid()) {Name = podcastName, FileKey = fileKey};
    }
}