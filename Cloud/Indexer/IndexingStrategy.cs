using RedditPodcastPoster.Configuration;

namespace Indexer;

public class IndexingStrategy(IDateTimeService dateTimeService) : IIndexingStrategy
{
    public bool ResolveYouTube()
    {
        // was  % 2
        var hour = dateTimeService.GetHour();
        return hour % 6 == 0 || hour == 11;
    }

    public bool ExpensiveYouTubeQueries()
    {
        var hour = dateTimeService.GetHour();
        return hour % 24 == 0 || hour == 11;
    }

    public bool ExpensiveSpotifyQueries()
    {
        var hour = dateTimeService.GetHour();
        return hour % 6 == 0 || hour == 11;
    }

    public bool IndexSpotify()
    {
        var hour = dateTimeService.GetHour();
        return hour % 2 == 0 || hour == 11;
    }
}