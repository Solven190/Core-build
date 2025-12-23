using System.Text.Json.Serialization;

namespace Ryujinx.UI.Common.Configuration.System
{
    [JsonConverter(typeof(JsonStringEnumConverter<Region>))]
    public enum Region
    {
        Japan,
        USA,
        Europe,
        Australia,
        China,
        Korea,
        Taiwan,
    }
}
