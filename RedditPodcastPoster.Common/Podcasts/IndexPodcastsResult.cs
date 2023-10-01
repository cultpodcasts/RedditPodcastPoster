using System.Text;

namespace RedditPodcastPoster.Common.Podcasts;

public record IndexPodcastsResult(IList<IndexPodcastResult> Results)
{
    public bool Success => Results.All(x => x.Success);

    public override string ToString()
    {
        var report = new StringBuilder();
        foreach (var indexPodcastResult in Results.Where(x => !x.Success))
        {
            report.Append(indexPodcastResult);
        }

        foreach (var indexPodcastResult in Results.Where(x => x.Success))
        {
            report.Append(indexPodcastResult);
        }

        if (report.Length == 0)
        {
            report.Append("Success.");
        }
        return report.ToString();
    }
}