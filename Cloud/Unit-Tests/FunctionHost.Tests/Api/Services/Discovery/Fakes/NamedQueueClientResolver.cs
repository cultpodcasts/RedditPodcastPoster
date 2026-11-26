using Azure.Storage.Queues;
using Moq.AutoMock.Resolvers;

namespace FunctionHost.Tests.Api.Services.Discovery.Fakes;

/// <summary>
/// Supplies a distinct <see cref="QueueClient"/> per constructor parameter. AutoMocker caches by type, so two
/// <see cref="QueueClient"/> arguments would otherwise be the same instance.
/// </summary>
internal sealed class NamedQueueClientResolver : IMockResolver
{
    public NamedQueueClientResolver(List<string> order)
    {
        Apply = new RecordingQueueClient("apply", order);
        Poison = new RecordingQueueClient("poison", order);
    }

    public RecordingQueueClient Apply { get; }

    public RecordingQueueClient Poison { get; }

    public void Resolve(MockResolutionContext context)
    {
        if (context.RequestType != typeof(QueueClient) || context.Value is not null)
        {
            return;
        }

        var name = context.ObjectGraphContext.ParameterInfo?.Name;
        context.Value = name switch
        {
            "queueClient" => Apply,
            "poisonQueueClient" => Poison,
            _ => null
        };
        if (context.Value is not null)
        {
            context.NoCache = true;
        }
    }
}
