using System;
using System.Text.Json.Serialization;

namespace CommonData
{
    public class LoginData
    {
        public UInt16 ServerId { get; set; }
        
        public string ClientInfo { get; set; }

        public string Name { get; set; }

        public int Role { get; set; }

        public int Sex { get; set; }

        [JsonPropertyName("modelID")]
        public UInt32 ModelId { get; set; }
    }
}
