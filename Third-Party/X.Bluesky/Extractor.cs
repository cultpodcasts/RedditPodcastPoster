using System.Collections.Immutable;
using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using X.Web.MetaExtractor;
using X.Web.MetaExtractor.LanguageDetectors;

namespace X.Bluesky;

public class Extractor : IExtractor
{
    private readonly string _defaultImage;
    private readonly ILanguageDetector _languageDetector;
    private readonly ILogger _logger;
    private readonly IPageContentLoader _pageContentLoader;

    public Extractor(
        string defaultImage,
        IPageContentLoader pageContentLoader,
        ILanguageDetector languageDetector,
        ILogger logger)
    {
        _defaultImage = defaultImage;
        _languageDetector = languageDetector;
        _logger = logger;
        _pageContentLoader = pageContentLoader;
    }

    public int MaxDescriptionLength { get; set; } = 300;

    public async Task<Metadata> ExtractAsync(Uri uri)
    {
        var html = await _pageContentLoader.LoadPageContentAsync(uri);
        var htmlDocument = CreateHtmlDocument(html);
        var title = ExtractTitle(htmlDocument);
        IReadOnlyCollection<string> keywords = ExtractKeywords(htmlDocument);
        IReadOnlyCollection<KeyValuePair<string, string>> metaTags = ExtractMetaTags(htmlDocument);
        var description = ExtractDescription(htmlDocument);
        IReadOnlyCollection<string> images = ExtractImages(htmlDocument, _defaultImage);
        var htmlPageLanguage = _languageDetector.GetHtmlPageLanguage(html);
        return new Metadata
        {
            Raw = html,
            Url = uri.ToString(),
            Title = title,
            Keywords = keywords,
            MetaTags = metaTags,
            Description = description,
            Images = images,
            Language = htmlPageLanguage
        };
    }

    private static string ExtractTitle(HtmlDocument document)
    {
        var title = ReadOpenGraphProperty(document, "og:title");
        if (string.IsNullOrWhiteSpace(title))
        {
            var htmlNode = document.DocumentNode.SelectSingleNode("//head/title");
            title = htmlNode != null ? HtmlDecode(htmlNode.InnerText) : string.Empty;
        }

        return title;
    }

    private static IReadOnlyCollection<string> ExtractKeywords(HtmlDocument document)
    {
        var htmlNode = document.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
        var str = htmlNode != null ? HtmlDecode(htmlNode?.Attributes["content"]?.Value ?? string.Empty) : string.Empty;
        return string.IsNullOrWhiteSpace(str)
            ? ImmutableArray<string>.Empty
            : str.Split(',').Select<string, string>((Func<string, string>) (o => o?.Trim()))
                .Where<string>((Func<string, bool>) (o => !string.IsNullOrWhiteSpace(o)))
                .Select<string, string>((Func<string, string>) (o => o)).ToImmutableList<string>();
    }

    private static IReadOnlyCollection<KeyValuePair<string, string>> ExtractMetaTags(
        HtmlDocument document)
    {
        List<KeyValuePair<string, string>> source1 = new();
        var source2 = document?.DocumentNode?.SelectNodes("//meta");
        if (source2 == null || !source2.Any<HtmlNode>())
        {
            return new List<KeyValuePair<string, string>>();
        }

        foreach (var htmlNode in source2)
        {
            var attributeValue1 = htmlNode.GetAttributeValue("content", "");
            var attributeValue2 = htmlNode.GetAttributeValue("property", "");
            var attributeValue3 = htmlNode.GetAttributeValue("name", "");
            if (!string.IsNullOrWhiteSpace(attributeValue2) || !string.IsNullOrWhiteSpace(attributeValue3))
            {
                source1.Add(new KeyValuePair<string, string>(OneOf(attributeValue2, attributeValue3), attributeValue1));
            }
        }

        return source1.ToImmutableList<KeyValuePair<string, string>>();
    }

    private static string ExtractDescription(HtmlDocument document)
    {
        var description = ReadOpenGraphProperty(document, "og:description");
        if (string.IsNullOrWhiteSpace(description))
        {
            var htmlNode = document.DocumentNode.SelectSingleNode("//meta[@name='description']");
            description = htmlNode != null
                ? HtmlDecode(htmlNode?.Attributes["content"]?.Value ?? string.Empty)
                : string.Empty;
        }

        return description;
    }

    private static IReadOnlyCollection<string> ExtractImages(
        HtmlDocument document,
        string defaultImage)
    {
        var str = ReadOpenGraphProperty(document, "og:image");
        if (!string.IsNullOrWhiteSpace(str))
        {
            return ImmutableList.Create<string>(str);
        }

        ImmutableList<string> immutableList = document.DocumentNode.Descendants("img")
            .Select<HtmlNode, string>((Func<HtmlNode, string>) (e => e.GetAttributeValue("src", null)))
            .Where<string>((Func<string, bool>) (src => !string.IsNullOrWhiteSpace(src))).ToImmutableList<string>();
        return !immutableList.Any<string>() && !string.IsNullOrWhiteSpace(defaultImage)
            ? ImmutableList.Create<string>(defaultImage)
            : (IReadOnlyCollection<string>) immutableList;
    }

    private static string OneOf(string a, string b)
    {
        return !string.IsNullOrWhiteSpace(b) ? b : a;
    }

    private static HtmlDocument CreateHtmlDocument(string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html ?? string.Empty);
        return htmlDocument;
    }

    private static string ReadOpenGraphProperty(HtmlDocument document, string name)
    {
        return HtmlDecode(document.DocumentNode.SelectSingleNode("//meta[@property='" + name + "']")
            ?.Attributes["content"]?.Value ?? string.Empty).Trim() ?? string.Empty;
    }

    private static string HtmlDecode(string text)
    {
        return (string.IsNullOrWhiteSpace(text) ? string.Empty : WebUtility.HtmlDecode(text)) ?? string.Empty;
    }
}