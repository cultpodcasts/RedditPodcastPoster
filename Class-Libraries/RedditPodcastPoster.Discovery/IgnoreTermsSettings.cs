namespace RedditPodcastPoster.Discovery;

public class IgnoreTermsSettings
{
    public string[]? IgnoreTerms { get; set; }

    public override string ToString()
    {
        if (IgnoreTerms == null)
        {
            return "No ignore terms found in configuration";
        }

        return string.Join(", ", IgnoreTerms.Select(x => $"'{x}'"));
    }
}