using System.Net;
using Api.Configuration;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;

namespace Api;

public abstract class BaseHttpFunction(
    IClientPrincipalFactory clientPrincipalFactory,
    IOptions<HostingOptions> hostingOptions,
    ILogger logger
)
{
    protected HostingOptions HostingOptions = hostingOptions.Value;

    protected async Task<HttpResponseData> HandleRequest(
        HttpRequestData req,
        string[] roles,
        Func<HttpRequestData, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        logger.LogInformation("{method} initiated for '{url}' / '{method}'.", 
            nameof(HandleRequest), req.Url, req.Method);
        var isAuthorised = false;
        var roleCtr = 0;
        var clientPrincipal = clientPrincipalFactory.Create(req);

        if (!HostingOptions.TestMode)
        {
            while (!isAuthorised && roleCtr < roles.Length)
            {
                var scope = roles[roleCtr++];
                if (clientPrincipal != null)
                {
                    isAuthorised = clientPrincipal.HasScope(scope) || HostingOptions.UserRoles.Contains(scope);
                }
            }
        }

        if (isAuthorised || roles.Contains("*"))
        {
            logger.LogInformation("{method} Authorised.", nameof(HandleRequest));
            var response = await authorised(req, clientPrincipal, ct);
            logger.LogInformation("{method} Response Gathered.", nameof(HandleRequest)); 
            return response;
        }

        logger.LogWarning("{method} Unauthorised.", nameof(HandleRequest));
        return await unauthorised(req, clientPrincipal, ct);
    }

    protected async Task<HttpResponseData> HandleRequest<T>(
        HttpRequestData req,
        string[] roles,
        T model,
        Func<HttpRequestData, T, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, T, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        logger.LogInformation("{method} initiated for '{url}' / '{method}'.", 
            nameof(HandleRequest), req.Url, req.Method);
        var isAuthorised = false;
        var roleCtr = 0;
        var clientPrincipal = clientPrincipalFactory.Create(req);

        while (!isAuthorised && roleCtr < roles.Length)
        {
            var scope = roles[roleCtr++];
            if (clientPrincipal != null)
            {
                isAuthorised = clientPrincipal.HasScope(scope) || HostingOptions.UserRoles.Contains(scope);
            }
        }

        if (isAuthorised)
        {
            logger.LogInformation("{method} Authorised.", nameof(HandleRequest));
            var response = await authorised(req, model, clientPrincipal, ct);
            logger.LogInformation("{method} Response Gathered.", nameof(HandleRequest));
            return response;
        }

        logger.LogWarning("{method} Unauthorised.", nameof(HandleRequest));
        return await unauthorised(req, model, clientPrincipal, ct);
    }

    protected async Task<HttpResponseData> HandlePublicRequest<T>(
    HttpRequestData req,
    T model,
    Func<HttpRequestData, T, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> authorised,
    Func<HttpRequestData, T, ClientPrincipal?, CancellationToken, Task<HttpResponseData>> unauthorised,
    CancellationToken ct)
    {
        logger.LogInformation("{method} initiated for '{url}' / '{method}'.",
            nameof(HandlePublicRequest), req.Url, req.Method);
        var clientPrincipal = clientPrincipalFactory.Create(req);
        var isAuthorised = clientPrincipal != null;
        if (isAuthorised)
        {
            logger.LogInformation("{method} Authorised.", nameof(HandlePublicRequest));
            var response = await authorised(req, model, clientPrincipal, ct);
            logger.LogInformation("{method} Response Gathered.", nameof(HandlePublicRequest));
            return response;
        }
        logger.LogWarning("{method} Unauthorised.", nameof(HandlePublicRequest));
        return await unauthorised(req, model, clientPrincipal, ct);
    }

    protected static Task<HttpResponseData> Unauthorised(HttpRequestData r, ClientPrincipal? _, CancellationToken c)
    {
        return r.CreateResponse(HttpStatusCode.Unauthorized).WithJsonBody(new {Message = "Unauthorised"}, c);
    }

    protected static Task<HttpResponseData> Unauthorised<T>(HttpRequestData r, T _, ClientPrincipal? cp,
        CancellationToken c)
    {
        return Unauthorised(r, cp, c);
    }
}