using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using RedditPodcastPoster.Auth0.Models;

namespace Api;

public abstract class BaseHttpFunction(
    IClientPrincipalFactory clientPrincipalFactory,
    IOptions<HostingOptions> hostingOptions,
    ILogger logger
)
{
    private readonly HostingOptions _hostingOptions = hostingOptions.Value;

    protected virtual async Task<HttpResponseData> HandleRequest(
        HttpRequestData req,
        string[] roles,
        Func<IHandlerContext, CancellationToken, Task<HttpResponseData>> authorised,
        Func<IHandlerContext, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        logger.LogInformation("{method} initiated for '{url}' / '{httpMethod}'.",
            nameof(HandleRequest), req.Url, req.Method);
        var isAuthorised = false;
        var roleCtr = 0;
        var clientPrincipal = await clientPrincipalFactory.CreateAsync(req);
        var ctx = new HandlerContext(req, clientPrincipal);

        if (!_hostingOptions.TestMode)
        {
            while (!isAuthorised && roleCtr < roles.Length)
            {
                var scope = roles[roleCtr++];
                if (clientPrincipal != null)
                {
                    isAuthorised = clientPrincipal.HasScope(scope) || _hostingOptions.UserRoles.Contains(scope);
                }
            }
        }

        if (isAuthorised || roles.Contains("*"))
        {
            logger.LogInformation("{method} Authorised.", nameof(HandleRequest));
            var response = await authorised(ctx, ct);
            logger.LogInformation("{method} Response Gathered.", nameof(HandleRequest));
            return response;
        }

        logger.LogWarning("{method} Unauthorised.", nameof(HandleRequest));
        return await unauthorised(ctx, ct);
    }

    protected virtual async Task<HttpResponseData> HandleRequest<T>(
        HttpRequestData req,
        string[] roles,
        T model,
        Func<IHandlerContext, T, CancellationToken, Task<HttpResponseData>> authorised,
        Func<IHandlerContext, T, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        logger.LogInformation("{method} initiated for '{url}' / '{httpMethod}'.",
            nameof(HandleRequest), req.Url, req.Method);
        var isAuthorised = false;
        var roleCtr = 0;
        var clientPrincipal = await clientPrincipalFactory.CreateAsync(req);
        var ctx = new HandlerContext(req, clientPrincipal);

        while (!isAuthorised && roleCtr < roles.Length)
        {
            var scope = roles[roleCtr++];
            if (clientPrincipal != null)
            {
                isAuthorised = clientPrincipal.HasScope(scope) || _hostingOptions.UserRoles.Contains(scope);
            }
        }

        if (isAuthorised)
        {
            logger.LogInformation("{method} Authorised.", nameof(HandleRequest));
            var response = await authorised(ctx, model, ct);
            logger.LogInformation("{method} Response Gathered.", nameof(HandleRequest));
            return response;
        }

        logger.LogWarning("{method} Unauthorised.", nameof(HandleRequest));
        return await unauthorised(ctx, model, ct);
    }

    protected virtual async Task<HttpResponseData> HandlePublicRequest<T>(
        HttpRequestData req,
        T model,
        Func<IHandlerContext, T, CancellationToken, Task<HttpResponseData>> authorised,
        Func<IHandlerContext, T, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        logger.LogInformation("{method} initiated for '{url}' / '{httpMethod}'.",
            nameof(HandlePublicRequest), req.Url, req.Method);
        var clientPrincipal = await clientPrincipalFactory.CreateAsync(req);
        var ctx = new HandlerContext(req, clientPrincipal);
        var isAuthorised = clientPrincipal != null;

        if (isAuthorised)
        {
            logger.LogInformation("{method} Authorised.", nameof(HandlePublicRequest));
            var response = await authorised(ctx, model, ct);
            logger.LogInformation("{method} Response Gathered.", nameof(HandlePublicRequest));
            return response;
        }

        logger.LogWarning("{method} Unauthorised.", nameof(HandlePublicRequest));
        return await unauthorised(ctx, model, ct);
    }

    protected static Task<HttpResponseData> Unauthorised(IHandlerContext ctx, CancellationToken ct)
    {
        return ctx.Json(HttpStatusCode.Unauthorized, new { Message = "Unauthorised" }, ct);
    }

    protected static Task<HttpResponseData> Unauthorised<T>(IHandlerContext ctx, T _, CancellationToken ct)
    {
        return Unauthorised(ctx, ct);
    }
}
