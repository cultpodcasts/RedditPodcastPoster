using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetPostFlair : BaseContainer
    {
        [JsonProperty("display")]
        public string Display { get; set; }

        [JsonProperty("order")]
        public List<string> Order { get; set; }  // List of flair template ids.  --Kris

        [JsonProperty("shortName")]
        public string ShortName { get; set; }

        [JsonProperty("styles")]
        public WidgetStyles Styles { get; set; }
    }
}
