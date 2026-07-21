using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Extensions;
using Api.Factories;
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;

namespace Api;

public abstract class MemoryProbedHttpBaseClass(
    IClientPrincipalFactory clientPrincipalFactory,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger logger)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    private readonly ILogger _logger = logger;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    protected override async Task<HttpResponseData> HandleRequest(
        HttpRequestData req,
        string[] roles,
        Func<HttpRequestData, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        return await HandleWithMemoryProbe(req,
            () => base.HandleRequest(req, roles, authorised, unauthorised, ct));
    }

    protected override async Task<HttpResponseData> HandleRequest<T>(
        HttpRequestData req,
        string[] roles,
        T model,
        Func<HttpRequestData, T, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, T, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        return await HandleWithMemoryProbe(req,
            () => base.HandleRequest(req, roles, model, authorised, unauthorised, ct));
    }

    protected override async Task<HttpResponseData> HandlePublicRequest<T>(
        HttpRequestData req,
        T model,
        Func<HttpRequestData, T, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, T, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        return await HandleWithMemoryProbe(req,
            () => base.HandlePublicRequest(req, model, authorised, unauthorised, ct));
    }

    private async Task<HttpResponseData> HandleWithMemoryProbe(HttpRequestData req,
        Func<Task<HttpResponseData>> execute)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(req.FunctionContext.FunctionDefinition.Name);

        try
        {
            var response = await execute();
            memoryProbe.End();
            return response;
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            _logger.LogError(ex, "Unhandled exception in {FunctionName}.",
                req.FunctionContext.FunctionDefinition.Name);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            return await errorResponse.WithJsonBody(
                new { error = "Internal server error" },
                req.FunctionContext.CancellationToken);
        }
    }
}
