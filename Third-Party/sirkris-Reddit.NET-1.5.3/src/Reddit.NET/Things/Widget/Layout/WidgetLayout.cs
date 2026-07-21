using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetLayout
    {
        [JsonProperty("idCardWidget")]
        public string IdCardWidget { get; set; }

        [JsonProperty("topbar")]
        public WidgetLayoutOrder Topbar { get; set; }

        [JsonProperty("sidebar")]
        public WidgetLayoutOrder Sidebar { get; set; }

        [JsonProperty("moderatorWidget")]
        public string ModeratorWidget { get; set; }
    }
}
