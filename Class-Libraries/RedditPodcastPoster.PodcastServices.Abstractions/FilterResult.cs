using System.Text;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public class FilterResult(IList<FilteredEpisode> filteredEpisodes)
{
    public IList<FilteredEpisode> FilteredEpisodes { get; init; } = filteredEpisodes;

    public override string ToString()
    {
        var report = new StringBuilder();
        report.Append("Removed due to terms: ");
        foreach (var (episode, terms) in FilteredEpisodes)
        {
            report.Append($"Title: '{episode.Title}' with Id: {episode.Id}' for terms '{string.Join(", ", terms)}'. ");
        }

        return report.ToString();
    }
}