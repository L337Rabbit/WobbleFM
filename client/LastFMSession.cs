using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace com.okitoki.wobblefm.client
{
    public class LastFMSession
    {
        [JsonPropertyName("name")]
        public string User { get; set; }
        
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("subscriber")]
        public int Subscriber { get; set; }
    }
}
