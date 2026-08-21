using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration
{
    [JsonConverter(typeof(JsonStringEnumConverter<TranslationLayer>))]
    public enum TranslationLayer 
    {
        MoltenVK,
        KosmicKrisp,
    }
}
