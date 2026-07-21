using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class S3UploadLease
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("fields")]
        public List<S3UploadLeaseField> Fields { get; set; }
    }
}
