using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Internal;

namespace Discovery;

public static class GeneratedDurableTaskExtensions
{
    private static readonly ITaskOrchestrator SingletonOrchestration = new Orchestration();

    [Function(nameof(Orchestration))]
    public static async Task<DiscoveryContext> Orchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var result = await SingletonOrchestration.RunAsync(context, context.GetInput<object>());
        return (DiscoveryContext) (result ?? default(DiscoveryContext)!);
    }

    /// <inheritdoc cref="IOrchestrationSubmitter.ScheduleNewOrchestrationInstanceAsync" />
    public static Task<string> ScheduleNewOrchestrationInstanceAsync(
        this IOrchestrationSubmitter client, object input, StartOrchestrationOptions? options = null)
    {
        return client.ScheduleNewOrchestrationInstanceAsync("Orchestration", input, options);
    }

    /// <inheritdoc cref="TaskOrchestrationContext.CallSubOrchestratorAsync" />
    public static Task<DiscoveryContext> CallOrchestrationAsync(
        this TaskOrchestrationContext context, object input, TaskOptions? options = null)
    {
        return context.CallSubOrchestratorAsync<DiscoveryContext>("Orchestration", input, options);
    }

    public static Task<DiscoveryContext> CallDiscoverAsync(this TaskOrchestrationContext ctx, DiscoveryContext input,
        TaskOptions? options = null)
    {
        return ctx.CallActivityAsync<DiscoveryContext>("Discover", input, options);
    }

    [Function(nameof(Discover))]
    public static async Task<DiscoveryContext> Discover([ActivityTrigger] DiscoveryContext input, string instanceId,
        FunctionContext executionContext)
    {
        ITaskActivity activity;
        try
        {
            activity = DurableActivityActivator.Create<Discover>(executionContext, nameof(Discover));
        }
        catch (Exception ex)
        {
            throw new DiscoveryOrchestrationIncompleteException(
                "Discover activity failed dependency injection activation.",
                ex);
        }

        TaskActivityContext context = new GeneratedActivityContext("Discover", instanceId);
        var result = await activity.RunAsync(context, input);
        return (DiscoveryContext) result!;
    }

    private sealed class GeneratedActivityContext : TaskActivityContext
    {
        public GeneratedActivityContext(TaskName name, string instanceId)
        {
            Name = name;
            InstanceId = instanceId;
        }

        public override TaskName Name { get; }

        public override string InstanceId { get; }
    }
}