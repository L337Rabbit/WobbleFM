using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using com.okitoki.wobblefm.client;

namespace com.okitoki.wobblefm.messages
{
    public class LastFMSessionMessage
    {
        [JsonPropertyName("session")]
        public LastFMSession Session { get; set; }
    }
}
