using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;
using System.Net.Http.Headers;
using X.Bluesky;
using X.Bluesky.Models;

namespace RedditPodcastPoster.Bluesky.Client;

public class EmbedCardBuilder(IHttpClientFactory httpClientFactory, Session session, ILogger logger)
{
    private readonly FileTypeHelper _fileTypeHelper = new(logger);

    public async Task<External> GetEmbedCard(EmbedCardRequest embedCardRequest)
    {
        var card = new External
        {
            Uri = embedCardRequest.Url.ToString(),
            Title = embedCardRequest.Title,
            Description = embedCardRequest.Description
        };

        if (embedCardRequest.ThumbUrl != null)
        {
            try
            {
                card.Thumb = await UploadImageAndSetThumbAsync(embedCardRequest.ThumbUrl);
                logger.LogInformation("EmbedCard created");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogError(ex, "Not-found when uploading-thumb-image: '{ThumbUrl}'", embedCardRequest.ThumbUrl);
            }
        }
       
        return card;
    }

    private async Task<Thumb?> UploadImageAndSetThumbAsync(Uri imageUrl)
    {
        var httpClient = httpClientFactory.CreateClient();

        var imgResp = await httpClient.GetAsync(imageUrl);
        imgResp.EnsureSuccessStatusCode();

        var mimeType = _fileTypeHelper.GetMimeTypeFromUrl(imageUrl);

        var imageContent = new StreamContent(await imgResp.Content.ReadAsStreamAsync());
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://bsky.social/xrpc/com.atproto.repo.uploadBlob")
        {
            Content = imageContent,
        };

        // Add the Authorization header with the access token to the request message
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);

        var response = await httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var blob = JsonConvert.DeserializeObject<BlobResponse>(json);

        var card = blob?.Blob;

        if (card != null)
        {
            // ToDo: fix it
            // This is hack for fix problem when Type is empty after deserialization
            card.Type = "blob"; 
        }

        return card;
    }
}