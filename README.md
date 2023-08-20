# Reddit-Podcast-Poster

This is a dotnet project to collate meta-data about Podcasts and post to a Reddit subreddit posts with links to those episodes.

Data is persisted in Cosmos-Db or a local-filesystem.

This code is licensed under the MIT license.

## Settings
This project depends on the follow user-secrets:

    {
      "spotify:ClientId": "xxxxx",
      "spotify:ClientSecret": "xxxx",
      "reddit:AppId": "xxxxx",
      "reddit:AppSecret": "xxxx",
      "reddit:RefreshToken": "xxxx",
      "youtube:ApiKey": "xxxx",
      "cosmosdb:Endpoint": "https:/xxxxx.documents.azure.com:443/",
      "cosmosdb:AuthKeyOrResourceToken": "xxxx",
      "cosmosdb:Container": "xxxxx",
      "cosmosdb:DatabaseId": "xxxx",
      "subreddit:SubredditName": "Name of the subreddit to post to",
      "subreddit:SubredditTitleMaxLength": 300 // the max-length of a title
    }

A Reddit.com refresh-key, to apply to the reddit:RefreshToken setting, can be generated using the source located here: https://github.com/sirkris/Reddit.NET

## Libraries used

- [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET/)
- [Reddit.Net](https://github.com/sirkris/Reddit.NET)
- [CommandLine](https://github.com/commandlineparser/commandline)
- [iTunesSearch](https://github.com/danesparza/iTunesSearch)