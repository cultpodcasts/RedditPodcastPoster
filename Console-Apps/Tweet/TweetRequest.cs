using CommandLine;

namespace Tweet;

public class TweetRequest
{
    [Value(0, MetaName = "podcast id", HelpText = "The Id of the podcast to tweet their last episode", Required = true)]
    public Guid PodcastId { get; set; }
}