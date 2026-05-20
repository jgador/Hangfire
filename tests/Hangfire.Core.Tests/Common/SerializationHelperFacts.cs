using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Common;
using Xunit;

namespace Hangfire.Core.Tests.Common
{
    public class SerializationHelperFacts
    {
        [Fact]
        public void Serialize_ReturnsNull_WhenValueIsNull()
        {
            Assert.Null(SerializationHelper.Serialize((string)null));
        }

        [Fact]
        public void Serialize_ReturnsCorrectResult_WhenValueIsString()
        {
            var result = SerializationHelper.Serialize("Simple string");

            Assert.Equal("\"Simple string\"", result);
        }

        [Fact]
        public void Serialize_ReturnsCorrectValue_WhenValueIsCustomObject()
        {
            var result = SerializationHelper.Serialize(new ClassA("B"));

            Assert.Equal(@"{""PropertyA"":""B""}", result);
        }

        [Fact]
        public void Serialize_ReturnsTypeMetadata_WhenOptionIsTypedInternal()
        {
            var result = SerializationHelper.Serialize<IClass>(new ClassA("B"), SerializationOption.TypedInternal);

            Assert.Equal(@"{""$type"":""Hangfire.Core.Tests.Common.SerializationHelperFacts+ClassA, Hangfire.Core.Tests"",""PropertyA"":""B""}", result);
        }

        [Fact, CleanSerializerSettings]
        public void Serialize_SerializesWithUserOptions_WhenOptionIsUser()
        {
            SerializationHelper.SetUserSerializerOptions(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var result = SerializationHelper.Serialize(new ClassA("A"), SerializationOption.User);

            Assert.Equal(@"{""propertyA"":""A""}", result);
        }

        [Fact, CleanSerializerSettings]
        public void Serialize_DoesNotSerializeWithUserOptions_WhenOptionIsInternal()
        {
            SerializationHelper.SetUserSerializerOptions(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var result = SerializationHelper.Serialize(new ClassA("A"));

            Assert.Equal(@"{""PropertyA"":""A""}", result);
        }

        [Fact]
        public void Deserialize_ReturnsCorrectObject_WhenTypeIsCustomClass()
        {
            var value = SerializationHelper.Deserialize(@"{""PropertyA"":""A""}", typeof(ClassA)) as ClassA;

            Assert.NotNull(value);
            Assert.Equal("A", value.PropertyA);
        }

        [Fact]
        public void Deserialize_ReturnsTypedInternalObject_WhenMetadataIsPresent()
        {
            var valueJson = @"{""$type"":""Hangfire.Core.Tests.Common.SerializationHelperFacts+ClassA, Hangfire.Core.Tests"",""PropertyA"":""A""}";

            var value = SerializationHelper.Deserialize<IClass>(valueJson, SerializationOption.TypedInternal);

            var customObj = Assert.IsType<ClassA>(value);
            Assert.Equal("A", customObj.PropertyA);
        }

        [Fact]
        public void DeserializeGeneric_RethrowsJsonException_WhenValueHasIncorrectFormat()
        {
            Assert.Throws<JsonException>(() => SerializationHelper.Deserialize<ClassA>("asdfaljsadkfh"));
        }

        [Fact]
        public void GetInternalOptions_SetsDefaultOptions()
        {
            var serializerOptions = SerializationHelper.GetInternalOptions();

            Assert.Equal(JsonIgnoreCondition.WhenWritingDefault, serializerOptions.DefaultIgnoreCondition);
            Assert.True(serializerOptions.PropertyNameCaseInsensitive);
            Assert.Equal(128, serializerOptions.MaxDepth);
        }

        private interface IClass
        {
        }

        private class ClassA : IClass
        {
            public ClassA(string propertyA)
            {
                PropertyA = propertyA;
            }

            public string PropertyA { get; }
        }
    }
}
