using System.Text.Json.Serialization;

namespace KaiHeiLa.API.Voice;

internal class ProduceParams
{
    public ProduceParams() { }
    public ProduceParams(Guid transportId)
    {
        TransportId = transportId;
    }
    
    [JsonPropertyName("appData")]
    public object AppData { get; set; }
    
    [JsonPropertyName("kind")]
    public string Kind { get; set; }
    
    [JsonPropertyName("peerId")]
    public string PeerId { get; set; }
    
    [JsonPropertyName("rtpParameters")]
    public RTPParameters RTPParameters { get; set; }
    
    [JsonPropertyName("transportId")]
    public Guid TransportId { get; set; }
}

internal class RTPParameters
{
    [JsonPropertyName("codecs")]
    public Codec[] Codecs { get; set; }
    
    [JsonPropertyName("encodings")]
    public Encoding[] Encodings { get; set; }
}

internal class Encoding
{
    [JsonPropertyName("ssrc")]
    public uint SSRC { get; set; }
}

internal class Codec
{
    [JsonPropertyName("channels")]
    public int Channels { get; set; }
    
    [JsonPropertyName("clockRate")]
    public int ClockRate { get; set; }
    
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; }
    
    [JsonPropertyName("parameters")]
    public Parameters Parameters { get; set; }
    
    [JsonPropertyName("payloadType")]
    public int PayloadType { get; set; }
}

internal class Parameters
{
    [JsonPropertyName("sprop-stereo")]
    public int SenderProduceStereo { get; set; }
}