using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Api
{
    public class Test(ILogger<Test> logger)
    {
        [Function("Test")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous,"get", "post")] HttpRequestData req)
        {
            logger.LogInformation("C# HTTP trigger function processed a request.");
            var success = req.CreateResponse(HttpStatusCode.OK);
            await success.WriteAsJsonAsync(SubmitUrlResponse.Successful("success"));
            return success;
        }
    }
}
