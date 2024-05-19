using Microsoft.Azure.Functions.Worker.Http;

namespace Api;

public static class ResponseExtensions
{
    public static async Task<HttpResponseData> WithJsonBody(
        this HttpResponseData response, 
        object body,
        CancellationToken ct)
    {
        await response.WriteAsJsonAsync(body, ct);
        return response;
    }
}