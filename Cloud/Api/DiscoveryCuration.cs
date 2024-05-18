using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class DiscoveryCuration
{
    private readonly ILogger<DiscoveryCuration> _logger;

    public DiscoveryCuration(ILogger<DiscoveryCuration> logger)
    {
        _logger = logger;
    }

    [Function("DiscoveryCuration")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct)
    {
        var success = req.CreateResponse(HttpStatusCode.OK);
        await success.WriteAsJsonAsync(new {Message = "Success"}, ct);
        return success;
    }
}