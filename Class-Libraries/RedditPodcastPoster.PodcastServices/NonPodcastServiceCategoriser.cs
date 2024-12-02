using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class NonPodcastServiceCategoriser(
    IPodcastRepository podcastRepository,
#pragma warning disable CS9113 // Parameter is unread.
    IHttpClientFactory httpClientFactory,
    ILogger<NonPodcastServiceCategoriser> logger
#pragma warning restore CS9113 // Parameter is unread.
) : INonPodcastServiceCategoriser
{
    public async Task<ResolvedNonPodcastServiceItem?> Resolve(Podcast? podcast, Uri url,
        IndexingContext indexingContext)
    {
        if (podcast == null)
        {
            List<Guid> matchingPodcastIds;
            if (IsBBC(url))
            {
                var wrappedIds = await podcastRepository
                    .GetAllBy(p => p.Episodes.Any(episode => episode.Urls.BBC == url), x => new {id = x.Id})
                    .ToListAsync();
                matchingPodcastIds = wrappedIds.Select(x => x.id).ToList();
            }
            else if (IsInternetArchive(url))
            {
                var wrappedIds = await podcastRepository
                    .GetAllBy(p => p.Episodes.Any(episode => episode.Urls.InternetArchive == url), x => new {id = x.Id})
                    .ToListAsync();
                matchingPodcastIds = wrappedIds.Select(x => x.id).ToList();
            }
            else
            {
                throw new InvalidOperationException("Unrecognised service");
            }

            if (matchingPodcastIds.Any())
            {
                if (matchingPodcastIds.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Found multiple podcasts with url '{url}'. Podcast-ids: {string.Join(", ", matchingPodcastIds)}.");
                }

                var podcastId = matchingPodcastIds.Single();
                podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
                IEnumerable<Episode> episodes;
                if (IsBBC(url))
                {
                    episodes = podcast!.Episodes.Where(x => x.Urls.BBC == url);
                }
                else
                {
                    episodes = podcast!.Episodes.Where(x => x.Urls.InternetArchive == url);
                }

                if (episodes.Count() > 1)
                {
                    throw new InvalidOperationException(
                        $"Found episodes with url '{url}'. Podcast-id: '{podcast.Id}'. Episode-ids: {string.Join(", ", episodes)}.");
                }

                return new ResolvedNonPodcastServiceItem(podcast, episodes.Single());
            }
        }

        return await CreateResolvedNonPodcastServiceItemFromUrl(podcast, url);
    }

    private bool IsInternetArchive(Uri url)
    {
        return url.Host.Contains("archive.org");
    }

    private bool IsBBC(Uri url)
    {
        return url.Host.Contains("bbc.co.uk");
    }

    private async Task<ResolvedNonPodcastServiceItem> CreateResolvedNonPodcastServiceItemFromUrl(
        Podcast? podcast,
        Uri uri)
    {
        var httpClient = httpClientFactory.CreateClient();
        var podcastsHomepageContent = await httpClient.GetAsync(uri);
        podcastsHomepageContent.EnsureSuccessStatusCode();

        var document = new HtmlDocument();
        document.Load(await podcastsHomepageContent.Content.ReadAsStreamAsync());
        var titleNodes = document.DocumentNode.SelectNodes("/html/head/title");
        if (!titleNodes.Any())
        {
            throw new InvalidOperationException($"Cannot extract title from '{uri}'.");
        }

        var titleNode = titleNodes.First();
        var title = titleNode.InnerText;
        var publisher = IsBBC(uri) ? "BBC" : "Internet Archive";

        return new ResolvedNonPodcastServiceItem(
            podcast,
            Title: title,
            Publisher: publisher,
            IsBBC: IsBBC(uri),
            IsInternetArchive: IsInternetArchive(uri),
            Url: uri);
    }
}