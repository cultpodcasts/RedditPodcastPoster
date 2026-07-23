using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Api.Extensions;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public sealed class HandlerContext(
    HttpRequestData request,
    ClientPrincipal? principal) : IHandlerContext
{
    public string? Subject => principal?.Subject;

    public string? Query(string name) => request.Query[name];

    public HttpResponseData Status(HttpStatusCode statusCode)
        => request.CreateResponse(statusCode);

    public Task<HttpResponseData> Json(HttpStatusCode statusCode, object body, CancellationToken cancellationToken)
        => request.CreateResponse(statusCode).WithJsonBody(body, cancellationToken);

    public HttpResponseData Ok() => Status(HttpStatusCode.OK);

    public Task<HttpResponseData> Ok(object body, CancellationToken cancellationToken)
        => Json(HttpStatusCode.OK, body, cancellationToken);

    public HttpResponseData Accepted() => Status(HttpStatusCode.Accepted);

    public Task<HttpResponseData> Accepted(object body, CancellationToken cancellationToken)
        => Json(HttpStatusCode.Accepted, body, cancellationToken);

    public HttpResponseData NotFound() => Status(HttpStatusCode.NotFound);

    public Task<HttpResponseData> NotFound(object body, CancellationToken cancellationToken)
        => Json(HttpStatusCode.NotFound, body, cancellationToken);

    public HttpResponseData BadRequest() => Status(HttpStatusCode.BadRequest);

    public Task<HttpResponseData> BadRequest(object body, CancellationToken cancellationToken)
        => Json(HttpStatusCode.BadRequest, body, cancellationToken);

    public HttpResponseData Conflict() => Status(HttpStatusCode.Conflict);

    public Task<HttpResponseData> Conflict(object body, CancellationToken cancellationToken)
        => Json(HttpStatusCode.Conflict, body, cancellationToken);

    public HttpResponseData InternalError() => Status(HttpStatusCode.InternalServerError);

    public Task<HttpResponseData> InternalError(object body, CancellationToken cancellationToken)
        => Json(HttpStatusCode.InternalServerError, body, cancellationToken);
}
