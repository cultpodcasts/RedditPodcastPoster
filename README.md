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