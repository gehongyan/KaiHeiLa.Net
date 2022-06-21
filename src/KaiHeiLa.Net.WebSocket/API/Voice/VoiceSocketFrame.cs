using System.Text.Json.Serialization;
using KaiHeiLa.Net.Converters;

namespace KaiHeiLa.API.Voice;

internal class VoiceSocketFrame
{
    // Frame type
    [JsonPropertyName("request")]
    public bool? Request { get; set; }
    [JsonPropertyName("response")]
    public bool? Response { get; set; }
    [JsonPropertyName("notification")]
    public bool? Notification { get; set; }
    
    [JsonPropertyName("method")]
    [JsonConverter(typeof(NullableVoiceSocketFrameTypeConverter))]
    public VoiceSocketFrameType? Type { get; set; }
    
    [JsonPropertyName("id")]
    public uint? Id { get; set; }
    
    [JsonPropertyName("ok")]
    public bool? Okay { get; set; }
    
    [JsonPropertyName("data")]
    public object Payload { get; set; }
}