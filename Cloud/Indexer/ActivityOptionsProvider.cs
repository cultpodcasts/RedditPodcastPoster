using Microsoft.Extensions.Options;

namespace Indexer;

public class ActivityOptionsProvider(IOptions<ActivityOptions> taskOptions) : IActivityOptionsProvider
{
    private ActivityOptions TaskOptions { get; } = taskOptions.Value;

    public bool RunIndex() => !DryRun.IsIndexDryRun && TaskOptions.RunIndex;
    public bool RunCategoriser() => !DryRun.IsCategoriserDryRun && TaskOptions.RunCategoriser;
    public bool RunPoster()=> !DryRun.IsPosterDryRun && TaskOptions.RunPoster;
    public bool RunPublisher() => !DryRun.IsPublisherDryRun && TaskOptions.RunPublisher;
    public bool RunTweet()=> !DryRun.IsTweetDryRun && TaskOptions.RunTweet;
    public bool RunBluesky()=> !DryRun.IsBlueskyDryRun && TaskOptions.RunBluesky;
}