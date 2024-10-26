namespace RedditPodcastPoster.PushSubscriptions;

public record DiscoveryNotification(int NumberOfReports, DateTime MinDateTime, int NumberOfResults)
{
    public override string ToString()
    {
        var plural = "s";
        if (NumberOfReports == 1)
        {
            plural = string.Empty;
        }

        var plural2 = "s";
        if (NumberOfResults == 1)
        {
            plural2 = string.Empty;
        }

        var fmt = "t";
        if (DateTime.UtcNow - MinDateTime >= TimeSpan.FromDays(1))
        {
            fmt = "dddd";
        }
        else if (DateTime.UtcNow - MinDateTime >= TimeSpan.FromDays(7))
        {
            fmt = "f";
        }

        var since = MinDateTime.ToString(fmt);
        return
            $"{NumberOfReports} report{plural} since {since}. {NumberOfResults} result{plural2}.";
    }
}