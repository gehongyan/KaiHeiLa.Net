using System.Text.Json.Serialization;
using KaiHeiLa.Net.Converters;

namespace KaiHeiLa.API.Rest;

internal class CreateOrRemoveGuildMuteDeafParams
{
    [JsonPropertyName("guild_id")]
    public ulong GuildId { get; set; }
    
    [JsonPropertyName("target_id")]
    public ulong UserId { get; set; }

    [JsonPropertyName("type")]
    public MuteOrDeafType Type { get; set; }
}