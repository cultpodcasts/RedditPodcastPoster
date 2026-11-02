using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Api.Middleware;

/// <summary>
/// Catches Isolated-worker input conversion failures (including <c>[FromBody]</c> JSON that cannot bind)
/// for HTTP triggers and returns 400 with <see cref="Dtos.ApiErrorResponse"/> instead of a host 500.
/// </summary>
public sealed class FunctionInputBindingBadRequestMiddleware(
    ILogger<FunctionInputBindingBadRequestMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (FunctionInputBindingFailure.IsInputBindingFailure(ex))
        {
            var httpRequest = await context.GetHttpRequestDataAsync();
            if (httpRequest is null)
            {
                throw;
            }

            logger.LogWarning(
                ex,
                "HTTP input binding failed for function {FunctionName}; returning 400.",
                context.FunctionDefinition.Name);

            var response = await FunctionInputBindingFailure.CreateBadRequestAsync(
                httpRequest,
                context.CancellationToken);
            FunctionInputBindingFailure.SetHttpInvocationResult(context, response);
        }
    }
}
