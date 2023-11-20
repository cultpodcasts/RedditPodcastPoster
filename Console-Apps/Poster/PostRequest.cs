using CommandLine;

namespace Poster;

public class PostRequest
{
    [Option('s', "publish-subjects", Required = false, HelpText = "Skip Tweet", Default = false)]
    public bool PublishSubjects { get; set; }

    [Option('t', "skip-tweet", Required = false, HelpText = "Skip Tweet", Default = false)]
    public bool SkipTweet { get; set; }

    [Option('p', "podcastid", Required = false, HelpText = "The Id of the podcast to add this episode to")]
    public Guid? PodcastId { get; set; }

    [Value(0, MetaName = "released-within-days", HelpText = "The number of days episodes to post have been released in",
        Required = false, Default = 3)]
    public int ReleasedWithin { get; set; }
}