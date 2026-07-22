using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers;

public interface IHandlerContext
{
    string? Subject { get; }

    string? Query(string name);

    HttpResponseData Status(HttpStatusCode statusCode);

    Task<HttpResponseData> Json(HttpStatusCode statusCode, object body, CancellationToken cancellationToken);

    HttpResponseData Ok();

    Task<HttpResponseData> Ok(object body, CancellationToken cancellationToken);

    HttpResponseData Accepted();

    Task<HttpResponseData> Accepted(object body, CancellationToken cancellationToken);

    HttpResponseData NotFound();

    Task<HttpResponseData> NotFound(object body, CancellationToken cancellationToken);

    HttpResponseData BadRequest();

    Task<HttpResponseData> BadRequest(object body, CancellationToken cancellationToken);

    HttpResponseData Conflict();

    Task<HttpResponseData> Conflict(object body, CancellationToken cancellationToken);

    HttpResponseData InternalError();

    Task<HttpResponseData> InternalError(object body, CancellationToken cancellationToken);
}
