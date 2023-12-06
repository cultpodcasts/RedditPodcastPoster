namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.RedditPost)]
public sealed class RedditPost : CosmosSelector
{
    public static readonly string PartitionKey = ModelType.RedditPost.ToString();

    public RedditPost(Guid id)
    {
        Id = id;
        ModelType = ModelType.RedditPost;
    }

    public string FullName { get; set; } = "";
    public string Author { get; set; } = "";
    public string RedditId { get; set; } = "";
    public DateTime Created { get; set; }
    public DateTime Edited { get; set; }
    public bool Removed { get; set; }
    public bool Spam { get; set; }
    public bool NSFW { get; set; }
    public int UpVotes { get; set; }
    public double UpVoteRatio { get; set; }
    public string Title { get; set; } = "";
    public int DownsVotes { get; set; }
    public string LinkFlairText { get; set; } = "";
    public string Url { get; set; } = "";
    public bool IsVideo { get; set; }
    public string Text { get; set; } = "";
    public string Html { get; set; } = "";

    public override string FileKey => FullName;
}