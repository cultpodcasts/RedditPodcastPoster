using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers;

public interface IHandlerContext
{
    string? Subject { get; }

    /// <summary>True when the caller's token grants <paramref name="scope"/> (Auth0 permission or OAuth scope).</summary>
    bool HasScope(string scope);

    /// <summary>
    /// Values of claim <paramref name="type"/>. A claim holding a JSON array of strings is expanded.
    /// Empty when the claim is absent.
    /// </summary>
    IReadOnlyList<string> ClaimValues(string type);

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
