using System.Text.Json.Serialization;
using KaiHeiLa.Net.Converters;

namespace KaiHeiLa.API.Rest;

internal class CreateOrRemoveChannelPermissionOverwriteParams
{
    [JsonPropertyName("channel_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong ChannelId { get; set; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(PermissionOverwriteTargetTypeConverter))]
    public PermissionOverwriteTargetType TargetType { get; set; }
    
    [JsonPropertyName("value")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong TargetId { get; set; }

    public CreateOrRemoveChannelPermissionOverwriteParams(ulong channelId, PermissionOverwriteTargetType targetType, ulong targetId)
    {
        ChannelId = channelId;
        TargetType = targetType;
        TargetId = targetId;
    }
}