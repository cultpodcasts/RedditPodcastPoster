using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Extensions;

namespace Api.Middleware;

/// <summary>
/// Shared helpers for turning Isolated-worker <c>[FromBody]</c> / input-converter failures into HTTP 400.
/// Binding runs before controllers; without this, the host surfaces those failures as 500.
/// </summary>
internal static class FunctionInputBindingFailure
{
    public const string DefaultMessage = "Request body could not be read.";

    public static bool IsInputBindingFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is FunctionInputConverterException)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        return await response.WithJsonBody(ApiErrorResponse.Failure(DefaultMessage), cancellationToken);
    }

    /// <summary>
    /// Assigns <paramref name="response"/> as the HTTP invocation result (single <c>$return</c> or named http output).
    /// </summary>
    public static void SetHttpInvocationResult(FunctionContext context, HttpResponseData response)
    {
        var httpOutputBinding = context.GetOutputBindings<HttpResponseData>()
            .FirstOrDefault(b => b.BindingType == "http" && b.Name != "$return");

        if (httpOutputBinding is not null)
        {
            httpOutputBinding.Value = response;
            return;
        }

        context.GetInvocationResult().Value = response;
    }
}
