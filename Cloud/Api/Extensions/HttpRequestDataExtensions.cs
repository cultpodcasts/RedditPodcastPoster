using System.Text;
using System.Text.Json;
using Api.Auth;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Extensions;

public static class HttpRequestDataExtensions
{
    public static bool HasScope(this HttpRequestData request, string scope)
    {
        if (!request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out var claimHeaders))
        {
            return false;
        }

        try
        {
            var claimHeader = claimHeaders.First();
            var decoded = Convert.FromBase64String(claimHeader);
            var json = Encoding.UTF8.GetString(decoded);
            var principal = JsonSerializer.Deserialize<ClientPrincipal>(json,
                new JsonSerializerOptions {PropertyNameCaseInsensitive = true});
            var scopeClaim =
                principal?.Claims.SingleOrDefault(x => x.Type == "permissions" && x.Value==scope);
            return scopeClaim != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}