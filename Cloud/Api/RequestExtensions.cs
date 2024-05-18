using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api;

public static class RequestExtensions
{
    public static Task<HttpResponseData> HandleRequest(
        this HttpRequestData req,
        string[] roles,
        Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> authorised,
        Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> unauthorised,
        CancellationToken ct)
    {
        var isAuthorised = false;
        var roleCtr = 0;
        while (!isAuthorised && roleCtr < roles.Length)
        {
            isAuthorised = req.HasScope(roles[roleCtr++]);
        }

        if (isAuthorised)
        {
            return authorised(req, ct);
        }

        return unauthorised(req, ct);
    }

    public static Task<HttpResponseData> HandleRequest<T>(
        this HttpRequestData req,
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
            isAuthorised = req.HasScope(roles[roleCtr++]);
        }

        if (isAuthorised)
        {
            return authorised(req, model, ct);
        }

        return unauthorised(req, model, ct);
    }
}