namespace RedditPodcastPoster.Subjects;

public interface IRecycledFlareIdProvider
{
    Guid GetId(string key);
    string[] GetKeys();
}