using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RedditPodcastPoster.BBC.DTOs;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.BBC.Extractors;

public partial class SoundsPageMetaDataExtractor : ISoundsPageMetaDataExtractor
{
    private const string PlayAreaExperienceId = "aod_play_area";
    private static readonly Regex NumericPrefix = CreateNumericPrefixRegex();

    public async Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse)
    {
        var document = new HtmlDocument();
        document.Load(await pageResponse.Content.ReadAsStreamAsync());

        var scripts = document.DocumentNode.SelectNodes("//script");
        var metaDataScript = scripts.FirstOrDefault(x =>
            x.Attributes["id"]?.Value == "__NEXT_DATA__" && x.Attributes["type"]?.Value == "application/json");
        if (metaDataScript != null)
        {
            var metaDataJson = metaDataScript.InnerText;
            var metaData = JsonSerializer.Deserialize<BBCSoundsMetaData>(metaDataJson);

            if (metaData != null)
            {
                var experiences = metaData.Properties.PageProperties.DehydratedState.Queries
                    .Single(x => x.QueryKey.Any(key => key.EndsWith(metaData.Query.ProgrammeId))).State
                    .ExperienceResponseWrapper
                    .ExperienceResponse;

                var playArea = experiences.FirstOrDefault(x =>
                                   string.Equals(x.Id, PlayAreaExperienceId, StringComparison.Ordinal))
                               ?? experiences[0];
                var currentProgramme = playArea.Programmes[0];

                var imageContainer =
                    document.DocumentNode.SelectNodes("//div[contains(@data-testid, 'episode-hero')]//picture/source");
                var maxImage = GetBestImage(imageContainer);

                return new NonPodcastServiceItemMetaData(
                    currentProgramme.Titles.Title,
                    currentProgramme.Synopses?.Description ?? string.Empty,
                    currentProgramme.Duration?.Length,
                    currentProgramme.Release?.Date,
                    maxImage,
                    currentProgramme.Guidance?.HasWarnings,
                    "BBC",
                    ResolveSeriesName(currentProgramme)
                );
            }
        }

        throw new InvalidOperationException($"Unable to obtain meta-data for BBC Sounds page '{url}'.");
    }

    private static string? ResolveSeriesName(Programme programme)
    {
        if (string.Equals(programme.Container?.Type, "brand", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(programme.Container?.Title))
        {
            return BbcSeriesName.FromProgrammeBrand(programme.Container.Title, programme.Titles.Title)
                   ?? programme.Container.Title.Trim();
        }

        return programme.Titles.SeriesName;
    }

    private static Uri? GetBestImage(HtmlNodeCollection? imageContainer)
    {
        if (imageContainer == null || imageContainer.Count == 0)
        {
            return null;
        }

        Uri? maxImage = null;
        var maxImages = imageContainer
            .SelectMany(x =>
            {
                var srcset = x.Attributes["srcset"]?.Value;
                return string.IsNullOrWhiteSpace(srcset)
                    ? []
                    : srcset.Split(",");
            })
            .Select(x =>
            {
                var y = x.Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (y.Length < 2 || !NumericPrefix.IsMatch(y[1]))
                {
                    return null;
                }

                return new
                {
                    Width = int.Parse(NumericPrefix.Match(y[1]).Groups["numericprefix"].Value),
                    Url = new Uri(y[0])
                };
            })
            .Where(x => x != null)
            .GroupBy(x => x!.Width)
            .OrderByDescending(x => x.Key)
            .FirstOrDefault()
            ?.ToList();
        if (maxImages != null && maxImages.Count != 0)
        {
            var jpg = maxImages.FirstOrDefault(x => x!.Url.ToString().EndsWith(".jpg"));
            var png = maxImages.FirstOrDefault(x => x!.Url.ToString().EndsWith(".png"));
            var webp = maxImages.FirstOrDefault(x => x!.Url.ToString().EndsWith(".webp"));
            var preferredImage = png ?? jpg ?? webp;
            if (preferredImage != null)
            {
                maxImage = preferredImage.Url;
            }
        }

        return maxImage;
    }

    [GeneratedRegex(@"^(?<numericprefix>\d+)")]
    private static partial Regex CreateNumericPrefixRegex();
}
