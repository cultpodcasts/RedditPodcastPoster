﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Paginators;

public class SpotifyQueryPaginator(
    ISpotifyClientWrapper spotifyClientWrapper,
    ILogger<SpotifyQueryPaginator> logger)
    : ISpotifyQueryPaginator
{
    public async Task<PodcastEpisodesResult> PaginateEpisodes(
        IPaginatable<SimpleEpisode>? pagedEpisodes,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                "Skipping '{nameofPaginateEpisodes}' as '{nameofSkipSpotifyUrlResolving}' is set.",
                nameof(PaginateEpisodes), nameof(indexingContext.SkipSpotifyUrlResolving));
            return new PodcastEpisodesResult(new List<SimpleEpisode>());
        }

        logger.LogInformation(
            "Running '{nameofPaginateEpisodes}'. Released-since: {releasedSince}.",
            nameof(PaginateEpisodes), indexingContext.ReleasedSince);


        if (pagedEpisodes?.Items == null)
        {
            return new PodcastEpisodesResult(new List<SimpleEpisode>());
        }

        var existingPagedEpisodes = await spotifyClientWrapper.Paginate(
            pagedEpisodes,
            indexingContext,
            new NullEpisodesLeadInPaginator(40, 3));

        var currentMoment = DateTime.Now.AddDays(2);
        var isInReverseTimeOrder = true;
        var ctr = 0;
        while (isInReverseTimeOrder && ctr < existingPagedEpisodes.Count)
        {
            var releaseDate = existingPagedEpisodes[ctr].GetReleaseDate();
            isInReverseTimeOrder = currentMoment >= releaseDate;
            if (isInReverseTimeOrder)
            {
                currentMoment = releaseDate;
            }

            ctr++;
        }

        var isExpensiveQueryFound = !isInReverseTimeOrder;

        logger.LogInformation(
            "Running '{nameofPaginateEpisodes}'. isExpensiveQueryFound: {isExpensiveQueryFound}.",
            nameof(PaginateEpisodes), isExpensiveQueryFound);

        var episodes = existingPagedEpisodes;

        if (indexingContext.ReleasedSince == null || isExpensiveQueryFound)
        {
            if (pagedEpisodes.Next != null && pagedEpisodes.Next.Contains("/show/"))
            {
                pagedEpisodes.Next = pagedEpisodes.Next.Replace("/show/", "/shows/");
            }

            var fetch = await spotifyClientWrapper.PaginateAll(pagedEpisodes, indexingContext);
            if (fetch != null)
            {
                episodes = fetch.ToList();
            }
        }
        else
        {
            if (episodes.Any())
            {
                var seenGrowth = true;
                while (
                    seenGrowth &&
                    episodes.Where(x => x != null).OrderByDescending(x => x.ReleaseDate).Last().GetReleaseDate() >=
                    indexingContext.ReleasedSince
                )
                {
                    var preCount = episodes.Count;
                    var items = await spotifyClientWrapper.Paginate(
                        pagedEpisodes,
                        indexingContext,
                        new SimpleEpisodePaginator(indexingContext.ReleasedSince, isInReverseTimeOrder)
                    );
                    if (items != null)
                    {
                        episodes = items.ToList();
                        seenGrowth = items != null && episodes.Count > preCount;
                    }
                }
            }
        }

        if (indexingContext.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.GetReleaseDate() > indexingContext.ReleasedSince).ToList();
        }

        return new PodcastEpisodesResult(episodes.Where(x => x != null).ToList(), isExpensiveQueryFound);
    }
}