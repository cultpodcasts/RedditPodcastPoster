using System.Net;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class DiscoveryCuration(
    IDiscoveryResultsService discoveryResultsService,
    ILogger<DiscoveryCuration> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions)
{
    [Function("DiscoveryCuration")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct)
    {
        return HandleRequest(
            req,
            ["curate"],
            async (r, c) =>
            {
                var result = await discoveryResultsService.Get(c);
                return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(result, c);
            },
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
        return HandleRequest(
            req,
            ["curate"],
            model,
            (r, m, c) =>
                r.CreateResponse(HttpStatusCode.OK).WithJsonBody(new {Message = "Success"}, c),
            (r, m, c) =>
                r.CreateResponse(HttpStatusCode.Unauthorized).WithJsonBody(new {Message = "Unauthorised"}, c),
            ct);
    }
}

public class Model
{
    private string Item1 { get; set; }
}