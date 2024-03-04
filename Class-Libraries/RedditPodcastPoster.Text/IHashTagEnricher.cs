namespace RedditPodcastPoster.Text;

public interface IHashTagEnricher
{
    (string Title, bool HashTagAdded) AddHashTag(string input, string match, string? replacement);
}