using Microsoft.Extensions.Options;

namespace Indexer;

public class ActivityOptionsProvider(IOptions<ActivityOptions> taskOptions) : IActivityOptionsProvider
{
    private ActivityOptions TaskOptions { get; } = taskOptions.Value;

    public bool RunIndex(out string reason)
    {
        reason =
            $"{nameof(DryRun.IsIndexDryRun)}= {DryRun.IsIndexDryRun}. {nameof(TaskOptions.RunIndex)}= {TaskOptions.RunIndex}";
        return !DryRun.IsIndexDryRun && TaskOptions.RunIndex;
    }

    public bool RunCategoriser(out string reason)
    {
        reason =
            $"{nameof(DryRun.IsCategoriserDryRun)}= {DryRun.IsCategoriserDryRun}. {nameof(TaskOptions.RunCategoriser)}= {TaskOptions.RunCategoriser}";
        return !DryRun.IsCategoriserDryRun && TaskOptions.RunCategoriser;
    }

    public bool RunPoster(out string reason)
    {
        reason =
            $"{nameof(DryRun.IsPosterDryRun)}= {DryRun.IsPosterDryRun}. {nameof(TaskOptions.RunPoster)}= {TaskOptions.RunPoster}";
        return !DryRun.IsPosterDryRun && TaskOptions.RunPoster;
    }

    public bool RunPublisher(out string reason)
    {
        reason =
            $"{nameof(DryRun.IsPublisherDryRun)}= {DryRun.IsPublisherDryRun}. {nameof(TaskOptions.RunPublisher)}= {TaskOptions.RunPublisher}";
        return !DryRun.IsPublisherDryRun && TaskOptions.RunPublisher;
    }

    public bool RunTweet(out string reason)
    {
        reason =
            $"{nameof(DryRun.IsTweetDryRun)}= {DryRun.IsTweetDryRun}. {nameof(TaskOptions.RunTweet)}= {TaskOptions.RunTweet}";
        return !DryRun.IsTweetDryRun && TaskOptions.RunTweet;
    }

    public bool RunBluesky(out string reason)
    {
        reason =
            $"{nameof(DryRun.IsBlueskyDryRun)}= {DryRun.IsBlueskyDryRun}. {nameof(TaskOptions.RunBluesky)}= {TaskOptions.RunBluesky}";
        return !DryRun.IsBlueskyDryRun && TaskOptions.RunBluesky;
    }
}