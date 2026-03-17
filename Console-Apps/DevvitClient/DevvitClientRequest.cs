using CommandLine;

namespace DevvitClient;

public class DevvitClientRequest
{
    [Option('p', "podcast-name", Required = true, HelpText = "Podcast name")]
    public string PodcastName { get; set; } = "";

    [Option('t', "title", Required = true, HelpText = "Episode title")]
    public string Title { get; set; } = "";

    [Option('d', "description", Required = true, HelpText = "Episode description")]
    public string Description { get; set; } = "";

    [Option('r', "release-date-time", Required = true, HelpText = "Release date/time in ISO format")]
    public string ReleaseDateTime { get; set; } = "";

    [Option('u', "duration", Required = true, HelpText = "Duration text, e.g. 1h 12m")]
    public string Duration { get; set; } = "";

    [Option("youtube", Required = false, HelpText = "YouTube episode link")]
    public string? Youtube { get; set; }

    [Option("spotify", Required = false, HelpText = "Spotify episode link")]
    public string? Spotify { get; set; }

    [Option("apple", Required = false, HelpText = "Apple Podcasts episode link")]
    public string? Apple { get; set; }

    [Option("image-url", Required = false, HelpText = "Optional episode image URL")]
    public string? ImageUrl { get; set; }

    [Option('s', "subreddit-name", Required = false, HelpText = "Override subreddit name")]
    public string? SubredditName { get; set; }

    [Option("flair-id", Required = false, HelpText = "Optional flair template id")]
    public string? FlairId { get; set; }

    [Option("flair-text", Required = false, HelpText = "Optional flair text")]
    public string? FlairText { get; set; }
}
