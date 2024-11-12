using CommandLine;

namespace Poster;

public class PostRequest
{
    [Option('y', "youtube-primary-post-service", Required = false,
        HelpText = "Post YouTube link where available regardless of podcast primary-post-service setting",
        Default = false)]
    public bool YouTubePrimaryPostService { get; set; }

    [Option('s', "publish-subjects", Required = false, HelpText = "Skip Tweet", Default = false)]
    public bool PublishSubjects { get; set; }

    [Option('g', "ignore-apple-grace-period", Required = false,
        HelpText = "Ignores a grace period that prevents posting when no apple-url is present for podcasts on Apple",
        Default = false)]
    public bool IgnoreAppleGracePeriod { get; set; }

    [Option('t', "skip-tweet", Required = false, HelpText = "Skip Tweet", Default = false)]
    public bool SkipTweet { get; set; }

    [Option('b', "skip-bluesky", Required = false, HelpText = "Skip Tweet", Default = false)]
    public bool SkipBluesky { get; set; }

    [Option('w', "skip-publish", Required = false, HelpText = "Skip Publish", Default = false)]
    public bool SkipPublish { get; set; }

    [Option('r', "skip-reddit", Required = false, HelpText = "Skip Reddit", Default = false)]
    public bool SkipReddit { get; set; }

    [Option('p', "podcastid", Required = false, HelpText = "The Id of the podcast post")]
    public Guid? PodcastId { get; set; }

    [Option('e', "episodeid", Required = false, HelpText = "The Id of the episode to post")]
    public Guid? EpisodeId { get; set; }

    [Value(0, MetaName = "released-within-days", HelpText = "The number of days episodes to post have been released in",
        Required = false, Default = 2)]
    public int ReleasedWithin { get; set; }

    [Option('f', "flip-when-ignored", Required = false, HelpText = "Flip ignored to false if true and post")]
    public bool FlipIgnored { get; set; }

    [Option('n', "name", Required = false, HelpText = "Name of the podcast (will perform partial-match")]
    public string? PodcastName { get; set; }
}