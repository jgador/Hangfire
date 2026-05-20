using System.Text.Json;
using Hangfire.Common;
using Xunit;

namespace Hangfire.Core.Tests
{
    public class GloabalConfigurationExtensionsFacts
    {
        [Fact, CleanSerializerSettings]
        public void UseSerializationOptions_AffectSerializationWithUserOptions()
        {
            GlobalConfiguration.Configuration.UseSerializerOptions(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var result = SerializationHelper.Serialize(new CustomClass { StringProperty = "Value" }, SerializationOption.User);
            Assert.Equal(@"{""stringProperty"":""Value""}", result);
        }

        [Fact, CleanSerializerSettings]
        public void UseSerializationOptionsWithCallback_AffectSerializationWithUserOptions()
        {
            GlobalConfiguration.Configuration.UseRecommendedSerializerOptions(options =>
            {
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

            var result = SerializationHelper.Serialize(new CustomClass { StringProperty = "Value" }, SerializationOption.User);
            Assert.Equal(@"{""stringProperty"":""Value""}", result);
        }

        public class CustomClass
        {
            public string StringProperty { get; set; }
        }
    }
}
