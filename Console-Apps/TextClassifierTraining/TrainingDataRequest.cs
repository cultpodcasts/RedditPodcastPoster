using CommandLine;

namespace TextClassifierTraining;

public class TrainingDataRequest
{
    [Option('s', "create-local-subreddit-repository", Required = false, HelpText = "Whether to create a local subreddit repository of posts",
        Default = false)]
    public bool CreateLocalSubredditPostsRepository { get; set; }
}