using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Indexer;

public static class GeneratedDurableTaskExtensions
{
    static readonly ITaskOrchestrator singletonHalfHourlyOrchestration = new HalfHourlyOrchestration();

    static readonly ITaskOrchestrator singletonHourlyOrchestration = new HourlyOrchestration();

    [Function(nameof(HalfHourlyOrchestration))]
    public static Task<IndexerContext> HalfHourlyOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return singletonHalfHourlyOrchestration.RunAsync(context, context.GetInput<object>())
            .ContinueWith(t => (IndexerContext)(t.Result ?? default(IndexerContext)!), TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <inheritdoc cref="IOrchestrationSubmitter.ScheduleNewOrchestrationInstanceAsync"/>
    public static Task<string> ScheduleNewHalfHourlyOrchestrationInstanceAsync(
        this IOrchestrationSubmitter client, object input, StartOrchestrationOptions? options = null)
    {
        return client.ScheduleNewOrchestrationInstanceAsync("HalfHourlyOrchestration", input, options);
    }

    /// <inheritdoc cref="TaskOrchestrationContext.CallSubOrchestratorAsync"/>
    public static Task<IndexerContext> CallHalfHourlyOrchestrationAsync(
        this TaskOrchestrationContext context, object input, TaskOptions? options = null)
    {
        return context.CallSubOrchestratorAsync<IndexerContext>("HalfHourlyOrchestration", input, options);
    }

    [Function(nameof(HourlyOrchestration))]
    public static Task<IndexerContext> HourlyOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return singletonHourlyOrchestration.RunAsync(context, context.GetInput<object>())
            .ContinueWith(t => (IndexerContext)(t.Result ?? default(IndexerContext)!), TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <inheritdoc cref="IOrchestrationSubmitter.ScheduleNewOrchestrationInstanceAsync"/>
    public static Task<string> ScheduleNewHourlyOrchestrationInstanceAsync(
        this IOrchestrationSubmitter client, object input, StartOrchestrationOptions? options = null)
    {
        return client.ScheduleNewOrchestrationInstanceAsync("HourlyOrchestration", input, options);
    }

    /// <inheritdoc cref="TaskOrchestrationContext.CallSubOrchestratorAsync"/>
    public static Task<IndexerContext> CallHourlyOrchestrationAsync(
        this TaskOrchestrationContext context, object input, TaskOptions? options = null)
    {
        return context.CallSubOrchestratorAsync<IndexerContext>("HourlyOrchestration", input, options);
    }

    public static Task<IndexerContext> CallTweetAsync(this TaskOrchestrationContext ctx, IndexerContext input, TaskOptions? options = null)
    {
        return ctx.CallActivityAsync<IndexerContext>("Tweet", input, options);
    }

    [Function(nameof(Tweet))]
    public static async Task<IndexerContext> Tweet([ActivityTrigger] IndexerContext input, string instanceId, FunctionContext executionContext)
    {
        ITaskActivity activity = ActivatorUtilities.CreateInstance<Tweet>(executionContext.InstanceServices);
        TaskActivityContext context = new GeneratedActivityContext("Tweet", instanceId);
        object? result = await activity.RunAsync(context, input);
        return (IndexerContext)result!;
    }

    public static Task<IndexerContext> CallBlueskyAsync(this TaskOrchestrationContext ctx, IndexerContext input, TaskOptions? options = null)
    {
        return ctx.CallActivityAsync<IndexerContext>("Bluesky", input, options);
    }

    [Function(nameof(Bluesky))]
    public static async Task<IndexerContext> Bluesky([ActivityTrigger] IndexerContext input, string instanceId, FunctionContext executionContext)
    {
        ITaskActivity activity = ActivatorUtilities.CreateInstance<Bluesky>(executionContext.InstanceServices);
        TaskActivityContext context = new GeneratedActivityContext("Bluesky", instanceId);
        object? result = await activity.RunAsync(context, input);
        return (IndexerContext)result!;
    }

    public static Task<IndexerContext> CallPosterAsync(this TaskOrchestrationContext ctx, IndexerContext input, TaskOptions? options = null)
    {
        return ctx.CallActivityAsync<IndexerContext>("Poster", input, options);
    }

    [Function(nameof(Poster))]
    public static async Task<IndexerContext> Poster([ActivityTrigger] IndexerContext input, string instanceId, FunctionContext executionContext)
    {
        ITaskActivity activity = ActivatorUtilities.CreateInstance<Poster>(executionContext.InstanceServices);
        TaskActivityContext context = new GeneratedActivityContext("Poster", instanceId);
        object? result = await activity.RunAsync(context, input);
        return (IndexerContext)result!;
    }

    public static Task<IndexerContext> CallCategoriserAsync(this TaskOrchestrationContext ctx, IndexerContext input, TaskOptions? options = null)
    {
        return ctx.CallActivityAsync<IndexerContext>("Categoriser", input, options);
    }

    [Function(nameof(Categoriser))]
    public static async Task<IndexerContext> Categoriser([ActivityTrigger] IndexerContext input, string instanceId, FunctionContext executionContext)
    {
        ITaskActivity activity = ActivatorUtilities.CreateInstance<Categoriser>(executionContext.InstanceServices);
        TaskActivityContext context = new GeneratedActivityContext("Categoriser", instanceId);
        object? result = await activity.RunAsync(context, input);
        return (IndexerContext)result!;
    }

    public static Task<IndexerContext> CallPublisherAsync(this TaskOrchestrationContext ctx, IndexerContext input, TaskOptions? options = null)
    {
        return ctx.CallActivityAsync<IndexerContext>("Publisher", input, options);
    }

    [Function(nameof(Publisher))]
    public static async Task<IndexerContext> Publisher([ActivityTrigger] IndexerContext input, string instanceId, FunctionContext executionContext)
    {
        ITaskActivity activity = ActivatorUtilities.CreateInstance<Publisher>(executionContext.InstanceServices);
        TaskActivityContext context = new GeneratedActivityContext("Publisher", instanceId);
        object? result = await activity.RunAsync(context, input);
        return (IndexerContext)result!;
    }

    public static Task<IndexerContext> CallIndexerAsync(this TaskOrchestrationContext ctx, IndexerContextWrapper input, TaskOptions? options = null)
    {
        return ctx.CallActivityAsync<IndexerContext>("Indexer", input, options);
    }

    [Function(nameof(Indexer))]
    public static async Task<IndexerContext> Indexer([ActivityTrigger] IndexerContextWrapper input, string instanceId, FunctionContext executionContext)
    {
        ITaskActivity activity = ActivatorUtilities.CreateInstance<Indexer>(executionContext.InstanceServices);
        TaskActivityContext context = new GeneratedActivityContext("Indexer", instanceId);
        object? result = await activity.RunAsync(context, input);
        return (IndexerContext)result!;
    }

    public static Task<IndexIds> CallIndexIdProviderAsync(this TaskOrchestrationContext ctx, object input, TaskOptions? options = null)
    {
        return ctx.CallActivityAsync<IndexIds>("IndexIdProvider", input, options);
    }

    [Function(nameof(IndexIdProvider))]
    public static async Task<IndexIds> IndexIdProvider([ActivityTrigger] object input, string instanceId, FunctionContext executionContext)
    {
        ITaskActivity activity = ActivatorUtilities.CreateInstance<IndexIdProvider>(executionContext.InstanceServices);
        TaskActivityContext context = new GeneratedActivityContext("IndexIdProvider", instanceId);
        object? result = await activity.RunAsync(context, input);
        return (IndexIds)result!;
    }

    sealed class GeneratedActivityContext : TaskActivityContext
    {
        public GeneratedActivityContext(TaskName name, string instanceId)
        {
            this.Name = name;
            this.InstanceId = instanceId;
        }

        public override TaskName Name { get; }

        public override string InstanceId { get; }
    }
}