using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace com.okitoki.wobblefm.messages
{
    public class LastFMTokenMessage
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}
