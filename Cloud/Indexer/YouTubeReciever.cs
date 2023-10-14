using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Indexer
{
    public class YouTubeReceiver
    {
        private readonly ILogger _logger;

        public YouTubeReceiver(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<YouTubeReceiver>();
        }

        [Function("YouTubeReceiver")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation($"{nameof(YouTubeReceiver)}");
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation(body);

            var response = req.CreateResponse(HttpStatusCode.NoContent);
            return response;
        }
    }
}
