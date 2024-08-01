using System.Net;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public abstract class BaseHttpFunction(
    IOptions<HostingOptions> hostingOptions,
    Logger<BaseHttpFunction> logger
    )
{
    protected HostingOptions HostingOptions = hostingOptions.Value;

    protected Task<HttpResponseData> HandleRequest(
        HttpRequestData req,
        string[] roles,
        Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        logger.LogInformation($"{nameof(HandleRequest)} initiated.");
        var isAuthorised = false;
        var roleCtr = 0;
        while (!isAuthorised && roleCtr < roles.Length)
        {
            var scope = roles[roleCtr++];
            isAuthorised = req.HasScope(scope) || HostingOptions.UserRoles.Contains(scope);
        }

        if (isAuthorised || roles.Contains("*"))
        {
            logger.LogInformation($"{nameof(HandleRequest)} Authorised.");
            var response = authorised(req, ct);
            logger.LogInformation($"{nameof(HandleRequest)} Response Gathered.");
            return response;
        }

        logger.LogWarning($"{nameof(HandleRequest)} Unauthorised.");
        return unauthorised(req, ct);
    }

    protected Task<HttpResponseData> HandleRequest<T>(
        HttpRequestData req,
        string[] roles,
        T model,
        Func<HttpRequestData, T, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, T, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        logger.LogInformation($"{nameof(HandleRequest)} initiated.");
        var isAuthorised = false;
        var roleCtr = 0;
        while (!isAuthorised && roleCtr < roles.Length)
        {
            var scope = roles[roleCtr++];
            isAuthorised = req.HasScope(scope) || HostingOptions.UserRoles.Contains(scope);
        }

        if (isAuthorised)
        {
            logger.LogInformation($"{nameof(HandleRequest)} Authorised.");
            var response = authorised(req, model, ct);
            logger.LogInformation($"{nameof(HandleRequest)} Response Gathered.");
            return response;
        }

        logger.LogWarning($"{nameof(HandleRequest)} Unauthorised.");
        return unauthorised(req, model, ct);
    }

    protected static Task<HttpResponseData> Unauthorised(HttpRequestData r, CancellationToken c)
    {
        return r.CreateResponse(HttpStatusCode.Unauthorized).WithJsonBody(new {Message = "Unauthorised"}, c);
    }

    protected static Task<HttpResponseData> Unauthorised<T>(HttpRequestData r, T _, CancellationToken c)
    {
        return Unauthorised(r, c);
    }
}