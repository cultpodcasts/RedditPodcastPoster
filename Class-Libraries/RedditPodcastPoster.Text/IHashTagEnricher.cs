namespace RedditPodcastPoster.Text;

public interface IHashTagEnricher
{
    (string, bool) AddHashTag(string input, string hashTagText);
}