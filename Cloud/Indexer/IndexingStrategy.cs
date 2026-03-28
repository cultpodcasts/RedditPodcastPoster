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

    public bool IsPrimaryPass(int pass, int totalPasses)
    {
        if (totalPasses < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPasses), "Total passes must be greater than 0.");
        }

        if (pass < 1 || pass > totalPasses)
        {
            throw new ArgumentOutOfRangeException(nameof(pass), $"Pass must be between 1 and {totalPasses}.");
        }

        var primaryPass = dateTimeService.GetHour() % totalPasses + 1;
        return pass == primaryPass;
    }
}