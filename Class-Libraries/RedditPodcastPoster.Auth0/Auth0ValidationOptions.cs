namespace RedditPodcastPoster.Auth0;

public class Auth0ValidationOptions
{
    public required string Audience { get; set; }
    public required string Domain { get; set; }
    public required string Issuer { get; set; }
}