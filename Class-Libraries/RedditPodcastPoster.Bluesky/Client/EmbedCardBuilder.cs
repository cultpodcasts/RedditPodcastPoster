using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using X.Bluesky;
using X.Bluesky.Models;

namespace RedditPodcastPoster.Bluesky.Client;

public class EmbedCardBuilder(IHttpClientFactory httpClientFactory, Session session, ILogger logger)
{
    private readonly FileTypeHelper _fileTypeHelper = new(logger);

    /// <summary>
    /// Create embed card
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public async Task<EmbedCard> GetEmbedCard(Uri url)
    {
        var extractor = new X.Web.MetaExtractor.Extractor();
        var metadata = await extractor.ExtractAsync(url);

        var card = new EmbedCard
        {
            Uri = url.ToString(),
            Title = metadata.Title,
            Description = metadata.Description
        };

        if (metadata.Images.Any())
        {
            var imgUrl = metadata.Images.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(imgUrl))
            {
                if (!imgUrl.Contains("://"))
                {
                    card.Thumb = await UploadImageAndSetThumbAsync(new Uri(url, imgUrl));
                }
                else
                {
                    card.Thumb = await UploadImageAndSetThumbAsync(new Uri(imgUrl));    
                }
                
                logger.LogInformation("EmbedCard created");
            }
        }

        return card;
    }


    public async Task<EmbedCard> GetEmbedCard(EmbedCardRequest embedCardRequest)
    {
        var card = new EmbedCard
        {
            Uri = embedCardRequest.Url.ToString(),
            Title = embedCardRequest.Title,
            Description = embedCardRequest.Description
        };

        if (embedCardRequest.ThumbUrl != null)
        {
            card.Thumb = await UploadImageAndSetThumbAsync(embedCardRequest.ThumbUrl);
            logger.LogInformation("EmbedCard created");
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