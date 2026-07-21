namespace RedditPodcastPoster.Text.Sanitisers;

public interface IHtmlSanitiser
{
    string Sanitise(string htmlDescription);
}
