using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos;
using Api.Models;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.SubmitUrl;
using Azure.Diagnostics;

namespace Api.Controllers;

public class SubmitUrlController(
    IPostSubmitUrlHandler postSubmitUrlHandler,
    IGetSubmitUrlLookupHandler getSubmitUrlLookupHandler,
    IPostSubmitUrlPrepareHandler postSubmitUrlPrepareHandler,
    IPostSubmitUrlExtractHandler postSubmitUrlExtractHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SubmitUrlController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("SubmitUrl")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] SubmitUrlRequest submitUrlModel,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate", "submit"],
            submitUrlModel,
            Handle,
            Unauthorised,
            ct);

    [Function("SubmitUrlLookup")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SubmitUrl")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate", "submit"],
            HandleLookup,
            Unauthorised,
            ct);

    [Function("SubmitUrlPrepare")]
    public Task<HttpResponseData> Prepare(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SubmitUrl/prepare")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] SubmitUrlPrepareRequest prepareRequest,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate", "submit"],
            prepareRequest,
            HandlePrepare,
            Unauthorised,
            ct);

    [Function("SubmitUrlExtract")]
    public Task<HttpResponseData> Extract(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SubmitUrl/extract")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] SubmitUrlExtractRequest extractRequest,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate", "submit"],
            extractRequest,
            HandleExtract,
            Unauthorised,
            ct);

    private Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubmitUrlRequest submitUrlModel,
        CancellationToken c)
    {
        if (!submitUrlModel.HasUsableHttpUrl())
        {
            return ctx.BadRequest(
                ApiErrorResponse.Failure("Url must be an absolute http or https URL"),
                c);
        }

        return postSubmitUrlHandler.Handle(ctx, submitUrlModel, c);
    }

    private Task<HttpResponseData> HandleLookup(
        IHandlerContext ctx,
        CancellationToken c)
    {
        if (!SubmitUrlRequest.TryParseUsableHttpUrl(ctx.Query("url"), out var parsed))
        {
            return ctx.BadRequest(
                ApiErrorResponse.Failure("Url must be an absolute http or https URL"),
                c);
        }

        return getSubmitUrlLookupHandler.Handle(ctx, parsed, c);
    }

    private Task<HttpResponseData> HandlePrepare(
        IHandlerContext ctx,
        SubmitUrlPrepareRequest prepareRequest,
        CancellationToken c)
    {
        if (!prepareRequest.HasUsableHttpUrl())
        {
            return ctx.BadRequest(
                ApiErrorResponse.Failure("Url must be an absolute http or https URL"),
                c);
        }

        return postSubmitUrlPrepareHandler.Handle(ctx, prepareRequest, c);
    }

    private Task<HttpResponseData> HandleExtract(
        IHandlerContext ctx,
        SubmitUrlExtractRequest extractRequest,
        CancellationToken c)
    {
        if (!extractRequest.HasUsableHttpUrl())
        {
            return ctx.BadRequest(
                ApiErrorResponse.Failure("Url must be an absolute http or https URL"),
                c);
        }

        if (string.IsNullOrWhiteSpace(extractRequest.Html))
        {
            return ctx.BadRequest(
                ApiErrorResponse.Failure("Html is required"),
                c);
        }

        return postSubmitUrlExtractHandler.Handle(ctx, extractRequest, c);
    }
}
