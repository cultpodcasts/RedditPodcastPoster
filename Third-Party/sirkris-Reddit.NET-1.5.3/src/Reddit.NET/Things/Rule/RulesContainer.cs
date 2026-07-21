using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class RulesContainer
    {
        [JsonProperty("rules")]
        public List<Rule> Rules { get; set; }

        [JsonProperty("site_rules")]
        public List<string> SiteRules { get; set; }

        [JsonProperty("site_rules_flow")]
        public List<NextStepReason> SiteRulesFlow { get; set; }
    }
}
