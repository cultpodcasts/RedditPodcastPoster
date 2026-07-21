using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Subjects.Providers;

public interface IRecycledFlareIdProvider
{
    Guid GetId(string key);
    string[] GetKeys();
}