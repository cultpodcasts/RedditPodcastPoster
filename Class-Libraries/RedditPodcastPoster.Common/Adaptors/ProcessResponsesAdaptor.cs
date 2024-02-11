using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.Adaptors;

public class ProcessResponsesAdaptor(ILogger<ProcessResponsesAdaptor> logger) : IProcessResponsesAdaptor
{
    private readonly ILogger<ProcessResponsesAdaptor> _logger = logger;

    public ProcessResponse CreateResponse(IList<ProcessResponse> matchingPodcastEpisodeResults)
    {
        var messages = new List<string>();
        var failures = false;
        if (matchingPodcastEpisodeResults.Any(x => !x.Success))
        {
            failures = true;
            messages.Add("Failures:");
            messages.AddRange(matchingPodcastEpisodeResults.Where(x => !x.Success).Select(x => x.Message));
        }

        if (matchingPodcastEpisodeResults.Any(x => x.Success))
        {
            if (matchingPodcastEpisodeResults.Any(x => x.Success && !string.IsNullOrWhiteSpace(x.Message)))
            {
                messages.Add("Success:");
                var resultMessages = matchingPodcastEpisodeResults
                    .Where(x => x.Success && !string.IsNullOrWhiteSpace(x.Message)).Select(x => x.Message);
                if (resultMessages.Any())
                {
                    messages.AddRange(resultMessages);
                }
            }
        }

        var result = string.Join(", ", messages);
        return failures ? ProcessResponse.Fail(result) : ProcessResponse.Successful(result);
    }
}