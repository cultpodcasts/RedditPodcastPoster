namespace RedditPodcastPoster.Text;

public interface IHtmlSanitiser
{
    string Sanitise(string htmlDescription);
}