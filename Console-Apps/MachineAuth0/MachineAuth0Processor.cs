using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EdgeApi;

namespace MachineAuth0;

public class MachineAuth0Processor(
    IApiClient apiClient,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<MachineAuth0Processor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Run()
    {
        await apiClient.Test();
    }
}