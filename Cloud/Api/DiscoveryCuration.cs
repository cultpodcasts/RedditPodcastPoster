using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class DiscoveryCuration(ILogger<DiscoveryCuration> logger)
{
    private readonly ILogger<DiscoveryCuration> _logger = logger;

    [Function("DiscoveryCuration")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct)
    {
        return req.HandleRequest(
            new[] {"curate"},
            (r, c) =>
                r.CreateResponse(HttpStatusCode.OK).WithJsonBody(new {Message = "Success"}, c),
            (r, c) =>
                r.CreateResponse(HttpStatusCode.Unauthorized).WithJsonBody(new {Message = "Unauthorised"}, c),
            ct);
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
            ["curate"],
            model,
            (r,m,  c) =>
                r.CreateResponse(HttpStatusCode.OK).WithJsonBody(new { Message = "Success" }, c),
            (r,m,  c) =>
                r.CreateResponse(HttpStatusCode.Unauthorized).WithJsonBody(new { Message = "Unauthorised" }, c),
            ct);
    }
}

public class Model
{
    private string Item1 { get; set; }
}