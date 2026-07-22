using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Extensions;
using Api.Factories;
using Azure.Diagnostics;

namespace Api.Controllers;

public class TestController(
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<TestController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("Test")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct)
    {
        logger.LogInformation($"{nameof(Run)} initiated.");

        return await HandleRequest(
            req,
            ["*"],
            async (ctx, c) =>
            {
                var success = await ctx.Ok(new {message = "success"}, c);
                return success;
            },
            async (ctx, c) =>
            {
                var failure = await ctx.Status(HttpStatusCode.Forbidden)
                    .WithJsonBody(new {message = "failure"}, c);
                return failure;
            },
            ct);
    }
}