using System.Text.Json;
using System.Text.Json.Serialization;
using KaiHeiLa.API.Voice;

namespace KaiHeiLa.Net.Converters;

internal class NullableVoiceSocketFrameTypeConverter : JsonConverter<VoiceSocketFrameType?>
{
    public override VoiceSocketFrameType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string method = reader.GetString();
        if (string.IsNullOrWhiteSpace(method))
            return null;
        return Enum.TryParse(method, true, out VoiceSocketFrameType value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(VoiceSocketFrameType));
    }

    public override void Write(Utf8JsonWriter writer, VoiceSocketFrameType? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        string method = value.ToString();
        method = method!.Length > 1 
            ? method[..1].ToLower() + method[1..] 
            : method.ToLower();
        writer.WriteStringValue(method);
    }
}