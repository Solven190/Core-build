using System.Text.Json.Serialization;

namespace Ryujinx.UI.Common.Configuration
{
    [JsonConverter(typeof(JsonStringEnumConverter<AudioBackend>))]
    public enum AudioBackend
    {
        Dummy,
        OpenAl,
        SoundIo,
        SDL2,
    }
}
