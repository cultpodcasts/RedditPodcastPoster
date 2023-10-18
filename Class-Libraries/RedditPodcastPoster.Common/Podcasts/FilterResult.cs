using System.Text;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class FilterResult
{
    public FilterResult(IList<(Episode, string[])> filteredEpisodes)
    {
        FilteredEpisodes = filteredEpisodes;
    }

    public IList<(Episode, string[])> FilteredEpisodes { get; init; }

    public override string ToString()
    {
        var report = new StringBuilder();
        report.AppendLine("Removed due to terms:");
        foreach (var (episode, terms) in FilteredEpisodes)
        {
            report.AppendLine(
                $"Title: '{episode.Title}' with Id: {episode.Id}' for terms '{string.Join(", ", terms)}'.");
        }

        return report.ToString();
    }
}