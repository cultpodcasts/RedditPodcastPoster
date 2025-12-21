using Api.Configuration;
using Api.Dtos;
using Api.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Api;

public class SubmitUrlController(
    ISubmitUrlHandler submitUrlHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SubmitUrlController> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    [Function("SubmitUrl")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] SubmitUrlRequest submitUrlModel,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["submit"], 
            submitUrlModel, 
            submitUrlHandler.Post, 
            Unauthorised, 
            ct);
}

