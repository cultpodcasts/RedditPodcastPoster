using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Indexer.Auth0;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Indexer;

public class SubmitUrl
{
    private readonly ILogger _logger;
    //private readonly ITokenValidator _tokenValidator;

    public SubmitUrl(
        //ITokenValidator tokenValidator,
        ILoggerFactory loggerFactory)
    {
        //_tokenValidator = tokenValidator;
        _logger = loggerFactory.CreateLogger<SubmitUrl>();
    }

    [Function("SubmitUrl")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")]
        HttpRequestData req)
    {
        foreach (var reqHeader in req.Headers)
        {
            _logger.LogInformation($"'{reqHeader.Key}'='{string.Join(", ", reqHeader.Value)}'");
        }


        //if (req.Headers.TryGetValues("Authorization", out var authHeaders))
        //{
        //    var authHeaderValue = authHeaders.FirstOrDefault();
        //    if (!string.IsNullOrWhiteSpace(authHeaderValue))
        //    {
        //        ClaimsPrincipal principal;
        //        principal = await _tokenValidator.ValidateTokenAsync(AuthenticationHeaderValue.Parse(authHeaderValue));
        //        if (principal == null)
        //        {
        //            return req.CreateResponse(HttpStatusCode.Unauthorized);
        //        }

        //        return req.CreateResponse(HttpStatusCode.OK);
        //    }
        //}

        //return req.CreateResponse(HttpStatusCode.Unauthorized);
        return req.CreateResponse(HttpStatusCode.OK);
    }
}