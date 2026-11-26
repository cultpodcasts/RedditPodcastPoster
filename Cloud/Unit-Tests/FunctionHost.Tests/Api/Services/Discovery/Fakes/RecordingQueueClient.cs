using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace FunctionHost.Tests.Api.Services.Discovery.Fakes;

/// <summary>
/// Records ensure-then-send calls without talking to Storage. The queue name is only the client identity.
/// </summary>
internal sealed class RecordingQueueClient : QueueClient
{
    private readonly string _role;
    private readonly List<string> _order;

    public RecordingQueueClient(string role, List<string> order)
        : base("UseDevelopmentStorage=true", role)
    {
        _role = role;
        _order = order;
    }

    public string? LastMessage { get; private set; }

    public override Task<Response> CreateIfNotExistsAsync(
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        _order.Add($"{_role}:exists");
        return Task.FromResult<Response>(null!);
    }

    public override Task<Response<SendReceipt>> SendMessageAsync(
        string messageText,
        CancellationToken cancellationToken = default)
    {
        _order.Add($"{_role}:send");
        LastMessage = messageText;
        return Task.FromResult<Response<SendReceipt>>(null!);
    }
}
