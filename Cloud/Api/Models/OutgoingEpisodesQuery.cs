namespace Api.Models;

public record OutgoingEpisodesQuery(
    int Days,
    bool Posted,
    bool Tweeted,
    bool BlueskyPosted)
{
    public static OutgoingEpisodesQuery Parse(
        string? days,
        string? posted,
        string? tweeted,
        string? blueskyPosted)
    {
        if (!bool.TryParse(tweeted, out var tweetedValue))
        {
            tweetedValue = false;
        }

        if (!bool.TryParse(posted, out var postedValue))
        {
            postedValue = false;
        }

        if (!bool.TryParse(blueskyPosted, out var blueskyPostedValue))
        {
            blueskyPostedValue = false;
        }

        if (!int.TryParse(days, out var daysValue))
        {
            daysValue = 7;
        }

        if (daysValue > 14)
        {
            daysValue = 14;
        }

        return new OutgoingEpisodesQuery(daysValue, postedValue, tweetedValue, blueskyPostedValue);
    }
}
