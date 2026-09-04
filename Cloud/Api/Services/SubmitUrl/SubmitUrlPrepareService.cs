using Api.Dtos;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace Api.Services.SubmitUrl;

public interface ISubmitUrlPrepareService
{
    Task<SubmitUrlPrepareResult> PrepareAsync(Uri url, CancellationToken cancellationToken);

    Task<SubmitUrlPrepareResult> ExtractAsync(Uri url, string html, CancellationToken cancellationToken);
}

public class SubmitUrlPrepareService(
    INonPodcastServiceAdapterResolver adapterResolver,
    ILogger<SubmitUrlPrepareService> logger) : ISubmitUrlPrepareService
{
    public Task<SubmitUrlPrepareResult> PrepareAsync(Uri url, CancellationToken cancellationToken) =>
        ExtractCoreAsync(url, html: null, cancellationToken);

    public Task<SubmitUrlPrepareResult> ExtractAsync(
        Uri url,
        string html,
        CancellationToken cancellationToken) =>
        ExtractCoreAsync(url, html, cancellationToken);

    private async Task<SubmitUrlPrepareResult> ExtractCoreAsync(
        Uri url,
        string? html,
        CancellationToken cancellationToken)
    {
        try
        {
            var adapter = adapterResolver.ForExtract(url);
            if (adapter is null)
            {
                return new SubmitUrlPrepareResult(
                    SubmitUrlPrepareStatus.BadRequest,
                    Message: "Url is not a supported streaming extract destination");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var meta = html is null
                ? await adapter.ExtractMetaData(url)
                : await adapter.ExtractMetaData(url, html);

            return new SubmitUrlPrepareResult(
                SubmitUrlPrepareStatus.Ok,
                SubmitUrlPrepareResponse.From(url, meta, adapter.Service));
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(ex, "HTML extract not supported for '{Url}'.", url);
            return new SubmitUrlPrepareResult(
                SubmitUrlPrepareStatus.BadRequest,
                Message: ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare/extract metadata for '{Url}'.", url);
            return new SubmitUrlPrepareResult(
                SubmitUrlPrepareStatus.Failed,
                Message: "Failure");
        }
    }
}
