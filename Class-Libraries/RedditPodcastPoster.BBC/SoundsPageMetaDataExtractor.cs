using HtmlAgilityPack;
using RedditPodcastPoster.BBC.DTOs;
using RedditPodcastPoster.PodcastServices.Abstractions;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.BBC;



public partial class SoundsPageMetaDataExtractor : ISoundsPageMetaDataExtractor
{
    private static readonly Regex NumericPrefix = CreateNumericPrefixRegex();

    public async Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse)
    {
        var document = new HtmlDocument();
        document.Load(await pageResponse.Content.ReadAsStreamAsync());

        var scripts = document.DocumentNode.SelectNodes("//script");
        var metaDataScript = scripts.Where(x => x.InnerText.TrimStart().StartsWith("window.__PRELOADED_STATE__")).FirstOrDefault();
        if (metaDataScript != null)
        {
            var metaDataJson = metaDataScript.InnerText.Substring(metaDataScript.InnerText.IndexOf("{") - 1).TrimEnd().TrimEnd(';');
            var metaData = JsonSerializer.Deserialize<BBCSoundsMetaData>(metaDataJson);

            if (metaData != null)
            {
                var imageContainer =
            document.DocumentNode.SelectNodes("//div[contains(@class, 'sc-c-herospace__imagery')]/picture/source");
                var maxImages = imageContainer
                    .Select(x => x.Attributes["srcset"].Value.Split(" "))
                    .Select(x => new
                    { Width = int.Parse(NumericPrefix.Match(x[1]).Groups["numericprefix"].Value), Url = new Uri(x[0]) })
                    .GroupBy(x => x.Width)
                    .OrderByDescending(x => x.Key)
                    .FirstOrDefault()
                    ?.ToList();
                Uri? maxImage = null;
                if (maxImages != null && maxImages.Any())
                {
                    var jpg = maxImages.Where(x => x.Url.ToString().EndsWith(".jpg")).FirstOrDefault();
                    var png = maxImages.Where(x => x.Url.ToString().EndsWith(".png")).FirstOrDefault();
                    var webp = maxImages.Where(x => x.Url.ToString().EndsWith(".webp")).FirstOrDefault();
                    var preferredImage = png ?? jpg ?? png;
                    if (preferredImage != null)
                    {
                        maxImage = preferredImage.Url;
                    }
                }

                return new NonPodcastServiceItemMetaData(
                    metaData.Programmes.CurrentProgramme.Titles.Title,
                    metaData.Programmes.CurrentProgramme.Synopses.Description,
                    metaData.Programmes.CurrentProgramme.Duration.Length,
                    metaData.Programmes.CurrentProgramme.Release.Date,
                    maxImage,
                    metaData.Programmes.CurrentProgramme.Guidance.HasWarnings
                    );
            }

        }

        throw new InvalidOperationException($"Unable to obtain meta-data for BBC Sounds pagee '{url}'.");
    }

    [GeneratedRegex(@"^(?<numericprefix>\d+)")]
    private static partial Regex CreateNumericPrefixRegex();
}




