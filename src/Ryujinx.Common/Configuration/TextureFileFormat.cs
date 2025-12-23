using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration
{
    [JsonConverter(typeof(JsonStringEnumConverter<TextureFileFormat>))]
    public enum TextureFileFormat
    {
        Dds,
        Png,
    }
}
