using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hangfire.Core.Tests
{
    public static class SerializerSettingsHelper
    {
        public static JsonSerializerOptions DangerousOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };
    }
}
