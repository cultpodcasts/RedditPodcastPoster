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
        var metaDataScript = scripts.Where(x =>
                x.Attributes["id"]?.Value == "__NEXT_DATA__" && x.Attributes["type"]?.Value == "application/json")
            .FirstOrDefault();
        if (metaDataScript != null)
        {
            var metaDataJson = metaDataScript.InnerText;
            var metaData = JsonSerializer.Deserialize<BBCSoundsMetaData>(metaDataJson);

            if (metaData != null)
            {
                var currentProgramme = metaData.Properties.PageProperties.DehydratedState.Queries
                    .Single(x => x.QueryKey.Any(x => x.EndsWith(metaData.Query.ProgrammeId))).State
                    .ExperienceResponseWrapper
                    .ExperienceResponse[0].Programmes[0];

                var imageContainer =
                    document.DocumentNode.SelectNodes("//div[contains(@data-testid, 'episode-hero')]//picture/source");
                var maxImage = GetBestImage(imageContainer);

                return new NonPodcastServiceItemMetaData(
                    currentProgramme.Titles.Title,
                    currentProgramme.Synopses.Description,
                    currentProgramme.Duration.Length,
                    currentProgramme.Release.Date,
                    maxImage,
                    currentProgramme.Guidance.HasWarnings
                );
            }
        }

        throw new InvalidOperationException($"Unable to obtain meta-data for BBC Sounds page '{url}'.");
    }

    private static Uri? GetBestImage(HtmlNodeCollection imageContainer)
    {
        Uri? maxImage = null;
        var maxImages = imageContainer
            .SelectMany(x => x.Attributes["srcset"].Value.Split(","))
            .Select(x =>
            {
                var y = x.Split(" ");
                return new
                {
                    Width = int.Parse(NumericPrefix.Match(y[1]).Groups["numericprefix"].Value),
                    Url = new Uri(y[0])
                };
            })
            .GroupBy(x => x.Width)
            .OrderByDescending(x => x.Key)
            .FirstOrDefault()
            ?.ToList();
        if (maxImages != null && maxImages.Any())
        {
            var jpg = maxImages.Where(x => x.Url.ToString().EndsWith(".jpg")).FirstOrDefault();
            var png = maxImages.Where(x => x.Url.ToString().EndsWith(".png")).FirstOrDefault();
            var webp = maxImages.Where(x => x.Url.ToString().EndsWith(".webp")).FirstOrDefault();
            var preferredImage = png ?? jpg ?? png;
            if (preferredImage != null) maxImage = preferredImage.Url;
        }

        return maxImage;
    }

    [GeneratedRegex(@"^(?<numericprefix>\d+)")]
    private static partial Regex CreateNumericPrefixRegex();
}