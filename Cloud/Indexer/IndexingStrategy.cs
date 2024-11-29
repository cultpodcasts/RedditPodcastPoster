using RedditPodcastPoster.Configuration;

namespace Indexer;

public class IndexingStrategy(IDateTimeService dateTimeService) : IIndexingStrategy
{
    public bool ResolveYouTube()
    {
        // was  % 2
        return dateTimeService.GetHour() % 6 == 0;
    }

    public bool ExpensiveYouTubeQueries()
    {
        return dateTimeService.GetHour() % 24 == 0;
    }

    public bool ExpensiveSpotifyQueries()
    {
        return dateTimeService.GetHour() % 6 == 0;
    }

    public bool IndexSpotify()
    {
        return dateTimeService.GetHour() % 2 == 0;
    }
}