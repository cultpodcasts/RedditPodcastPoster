using Microsoft.Extensions.Options;

namespace Indexer;

public class ActivityOptionsProvider(IOptions<ActivityOptions> taskOptions) : IActivityOptionsProvider
{
    private ActivityOptions TaskOptions { get; } = taskOptions.Value;

    public bool RunIndex(out string reason)
    {
        reason = $"IsIndexDryRun= {DryRun.IsIndexDryRun}. RunIndex= {TaskOptions.RunIndex}";
        return !DryRun.IsIndexDryRun && TaskOptions.RunIndex;
    }

    public bool RunCategoriser(out string reason)
    {
        reason = $"IsCategoriserDryRun= {DryRun.IsCategoriserDryRun}. RunCategoriser= {TaskOptions.RunCategoriser}";
        return !DryRun.IsCategoriserDryRun && TaskOptions.RunCategoriser;
    }

    public bool RunPoster(out string reason)
    {
        reason = $"IsPosterDryRun= {DryRun.IsPosterDryRun}. RunPoster= {TaskOptions.RunPoster}";
        return !DryRun.IsPosterDryRun && TaskOptions.RunPoster;
    }

    public bool RunPublisher(out string reason)
    {
        reason = $"IsPublisherDryRun= {DryRun.IsPublisherDryRun}. RunPublisher= {TaskOptions.RunPublisher}";
        return !DryRun.IsPublisherDryRun && TaskOptions.RunPublisher;
    }

    public bool RunTweet(out string reason)
    {
        reason = $"IsTweetDryRun= {DryRun.IsTweetDryRun}. RunTweet= {TaskOptions.RunTweet}";
        return !DryRun.IsTweetDryRun && TaskOptions.RunTweet;
    }

    public bool RunBluesky(out string reason)
    {
        reason = $"IsBlueskyDryRun= {DryRun.IsBlueskyDryRun}. RunBluesky= {TaskOptions.RunBluesky}";
        return !DryRun.IsBlueskyDryRun && TaskOptions.RunBluesky;
    }
}