# Reddit-Podcast-Poster

This is a dotnet project to collate meta-data about Podcasts and post to a Reddit subreddit posts with links to those episodes.

Data is persisted in Cosmos-Db or a local-filesystem.

This code is licensed under the MIT license.

## Settings
This project depends on the follow user-secrets:

	{
	  "spotify:ClientId": "xxxxxxx",
	  "spotify:ClientSecret": "xxxxxxx",
	  "reddit:AppId": "xxxxxxx",
	  "reddit:AppSecret": "xxxxxxx",
	  "reddit:RefreshToken": "xxxxxxx",
	  "youtube:ApiKey": "xxxxxxx",
	  "cosmosdb:Endpoint": "https://xxxxxxx.documents.azure.com:443/",
	  "cosmosdb:AuthKeyOrResourceToken": "xxxxxxx",
	  "cosmosdb:Container": "xxxxxxx",
	  "cosmosdb:DatabaseId": "xxxxxxx",
	  "cosmosdb:UseGateWay": true,
	  "subreddit:SubredditName": "xxxxxxx",
	  "subreddit:SubredditTitleMaxLength": 300,
	  "cloudflare:AccountId": "xxxxxxx",
	  "cloudflare:R2AccessKey": "xxxxxxx",
	  "cloudflare:R2SecretKey": "xxxxxxx",
	  "cloudflare:BucketName": "xxxxxxx",
	  "cloudflare:HomepageKey": "xxxxxxx",
	  "twitter:ConsumerKey": "xxxxxxx",
	  "twitter:ConsumerSecret": "xxxxxxx",
	  "twitter:AccessToken": "xxxxxxx",
	  "twitter:AccessTokenSecret": "xxxxxxxx",
	  "delayedYouTubePublication:EvaluationThreshold": "4:00:00",
	}
A Reddit.com refresh-key, to apply to the reddit:RefreshToken setting, can be generated using the source located here: https://github.com/sirkris/Reddit.NET and the dotnetcore app demonstrated here: https://www.youtube.com/watch?v=xlWhLyVgN2s

## Libraries used

- [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET/)
- [Reddit.Net](https://github.com/sirkris/Reddit.NET)
- [CommandLine](https://github.com/commandlineparser/commandline)
- [iTunesSearch](https://github.com/danesparza/iTunesSearch)