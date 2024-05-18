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
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct)
    {
        return req.HandleRequest(
            new[] {"curate"},
            async (r, c) =>
            {
                var success = r.CreateResponse(HttpStatusCode.OK);
                await success.WriteAsJsonAsync(new {Message = "Success"}, c);
                return success;
            }, async (r, c) =>
            {
                var failure = r.CreateResponse(HttpStatusCode.Unauthorized);
                await failure.WriteAsJsonAsync(new {Message = "Unauthorised"}, c);
                return failure;
            }, ct);
    }

    [Function("DiscoveryCurationWithModel")]
    public Task<HttpResponseData> RunWithModel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] Model model,
        CancellationToken ct)
    {
        return req.HandleRequest(
            new[] {"curate"},
            model,
            async (r, m, c) =>
            {
                var success = r.CreateResponse(HttpStatusCode.OK);
                await success.WriteAsJsonAsync(new {Message = "Success"}, c);
                return success;
            }, async (r, m, c) =>
            {
                var failure = r.CreateResponse(HttpStatusCode.Unauthorized);
                await failure.WriteAsJsonAsync(new {Message = "Unauthorised"}, c);
                return failure;
            }, ct);
    }
}

public class Model
{
    private string Item1 { get; set; }
}