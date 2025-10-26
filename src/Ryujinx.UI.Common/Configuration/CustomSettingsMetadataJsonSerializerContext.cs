using Ryujinx.UI.Common.Models;
using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(CustomSettingsModel))]
    public partial class CustomSettingsMetadataJsonSerializerContext : JsonSerializerContext
    {
    }
}
