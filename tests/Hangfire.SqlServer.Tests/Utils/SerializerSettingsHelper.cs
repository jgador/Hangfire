using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hangfire.SqlServer.Tests
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
