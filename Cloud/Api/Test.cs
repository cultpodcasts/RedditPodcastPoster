using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Api;

public class Test(IOptions<CosmosDbSettings> settings, ILogger<Test> logger)
{
    private readonly CosmosDbSettings _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

    [Function("Test")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
        HttpRequestData req,
        FunctionContext executionContext)
    {
        var claimsHeader = req.Headers.GetValues("X-MS-CLIENT-PRINCIPAL").SingleOrDefault();
        if (claimsHeader != null)
        {
            var decoded = Convert.FromBase64String(claimsHeader);
            var json = Encoding.UTF8.GetString(decoded);
            var principal = JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var principalJson = JsonSerializer.Serialize(principal);
            var success = req.CreateResponse(HttpStatusCode.OK);
            await success.WriteAsJsonAsync(SubmitUrlResponse.Successful($"principle: '{principalJson}'."));
            return success;

        }
        var failure = req.CreateResponse(HttpStatusCode.OK);
        await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("No principle."));
        return failure;

    }
}

public class ClientPrincipal
{
    [JsonPropertyName("auth_typ")]
    public string IdentityProvider { get; set; }
    [JsonPropertyName("name_typ")]
    public string NameClaimType { get; set; }
    [JsonPropertyName("role_typ")]
    public string RoleClaimType { get; set; }
    [JsonPropertyName("claims")]
    public IEnumerable<ClientPrincipalClaim> Claims { get; set; }
}

public class ClientPrincipalClaim
{
    [JsonPropertyName("typ")]
    public string Type { get; set; }
    [JsonPropertyName("val")]
    public string Value { get; set; }
}