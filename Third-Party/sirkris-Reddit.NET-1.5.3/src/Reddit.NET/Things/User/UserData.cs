﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Reddit.Things
{
    [Serializable]
    public class UserData : BaseData
    {
        [JsonProperty("children")]
        public List<UserChild> Children { get; set; }

        [JsonProperty("facets")]
        public object Facets { get; set; }  // TODO - Find out what this is.  It's used by Models.Search.GetSearch.  Comes up empty in tests even when include_facets is true.  --Kris
    }
}
