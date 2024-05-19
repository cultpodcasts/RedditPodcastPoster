using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;

namespace Api;

public abstract class BaseHttpFunction(IOptions<HostingOptions> hostingOptions)
{
    protected HostingOptions HostingOptions = hostingOptions.Value;

    protected Task<HttpResponseData> HandleRequest(
        HttpRequestData req,
        string[] roles,
        Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        var isAuthorised = false;
        var roleCtr = 0;
        while (!isAuthorised && roleCtr < roles.Length)
        {
            var scope = roles[roleCtr++];
            isAuthorised = req.HasScope(scope) || HostingOptions.UserRoles.Contains(scope);
        }

        if (isAuthorised || roles.Contains("*"))
        {
            return authorised(req, ct);
        }

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
        var isAuthorised = false;
        var roleCtr = 0;
        while (!isAuthorised && roleCtr < roles.Length)
        {
            var scope = roles[roleCtr++];
            isAuthorised = req.HasScope(scope) || HostingOptions.UserRoles.Contains(scope);
        }

        if (isAuthorised)
        {
            return authorised(req, model, ct);
        }

        return unauthorised(req, model, ct);
    }
}