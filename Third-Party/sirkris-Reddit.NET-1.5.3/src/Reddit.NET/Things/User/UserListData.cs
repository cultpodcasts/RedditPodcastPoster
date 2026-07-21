using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class UserListData
    {
        [JsonProperty("children")]
        public List<UserListChild> Children { get; set; }
    }
}
