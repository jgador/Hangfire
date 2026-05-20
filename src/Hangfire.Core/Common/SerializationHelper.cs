// This file is part of Hangfire. Copyright © 2019 Hangfire OÜ.
// 
// Hangfire is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as 
// published by the Free Software Foundation, either version 3 
// of the License, or any later version.
// 
// Hangfire is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public 
// License along with Hangfire. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Hangfire.Annotations;

namespace Hangfire.Common
{
    public enum SerializationOption
    {
        /// <summary>
        /// For internal data using isolated settings that can't be changed from user code.
        /// </summary>
        Internal,

        /// <summary>
        /// For internal data using isolated settings with type information
        /// that can't be changed from user code.
        /// </summary>
        TypedInternal,

        /// <summary>
        /// For user data like arguments and parameters, configurable via <see cref="SerializationHelper.SetUserSerializerOptions"/>.
        /// </summary>
        User
    }

    /// <summary>
    /// Provides methods to serialize/deserialize data with Hangfire default settings. 
    /// Isolates internal serialization process from user interference.
    /// </summary>
    public static class SerializationHelper
    {
        private const string TypePropertyName = "$type";

        private static readonly Lazy<JsonSerializerOptions> InternalSerializerOptions =
            new Lazy<JsonSerializerOptions>(GetInternalOptions, LazyThreadSafetyMode.PublicationOnly);

        private static readonly Lazy<JsonSerializerOptions> DefaultUserSerializerOptions =
            new Lazy<JsonSerializerOptions>(GetDefaultUserOptions, LazyThreadSafetyMode.PublicationOnly);

        private static JsonSerializerOptions _userSerializerOptions;

        /// <summary>
        /// Serializes data with <see cref="SerializationOption.Internal"/> option.
        /// Use this method to serialize internal data. Using isolated settings that can't be changed from user code.
        /// </summary>
        public static string Serialize<T>([CanBeNull] T value)
        {
            return Serialize(value, SerializationOption.Internal);
        }

        /// <summary>
        /// Serializes data with specified option. 
        /// Use <see cref="SerializationOption.Internal"/> option to serialize internal data.
        /// Use <see cref="SerializationOption.TypedInternal"/> option if you need to store type information.
        /// Use <see cref="SerializationOption.User"/> option to serialize user data like arguments and parameters,
        /// configurable via <see cref="SetUserSerializerOptions"/>.
        /// </summary>
        public static string Serialize<T>([CanBeNull] T value, SerializationOption option)
        {
            return Serialize(value, typeof(T), option);
        }

        /// <summary>
        /// Serializes data with specified option. 
        /// Use <see cref="SerializationOption.Internal"/> option to serialize internal data.
        /// Use <see cref="SerializationOption.TypedInternal"/> option if you need to store type information.
        /// Use <see cref="SerializationOption.User"/> option to serialize user data like arguments and parameters,
        /// configurable via <see cref="SetUserSerializerOptions"/>.
        /// </summary>
        public static string Serialize([CanBeNull] object value, [CanBeNull] Type type, SerializationOption option)
        {
            if (value == null) return null;

            var serializerOptions = GetSerializerOptions(option);
            var valueType = type ?? value.GetType();

            if (UsesTypeMetadata(serializerOptions, valueType))
            {
                return SerializeWithTypeMetadata(value, serializerOptions);
            }

            return JsonSerializer.Serialize(value, valueType, serializerOptions);
        }

        /// <summary>
        /// Deserializes data with <see cref="SerializationOption.Internal"/> option.
        /// Use this method to deserialize internal data. Using isolated settings that can't be changed from user code.
        /// </summary>
        public static object Deserialize([CanBeNull] string value, [NotNull] Type type)
        {
            return Deserialize(value, type, SerializationOption.Internal);
        }

        /// <summary>
        /// Deserializes data with specified option. 
        /// Use <see cref="SerializationOption.Internal"/> to deserialize internal data.
        /// Use <see cref="SerializationOption.TypedInternal"/> if deserializable internal data has type names information.
        /// Use <see cref="SerializationOption.User"/> to deserialize user data like arguments and parameters, 
        /// configurable via <see cref="SetUserSerializerOptions"/>.
        /// </summary>
        public static object Deserialize([CanBeNull] string value, [NotNull] Type type, SerializationOption option)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (value == null) return null;

            return JsonSerializer.Deserialize(value, type, GetSerializerOptions(option));
        }

        /// <summary>
        /// Deserializes data with <see cref="SerializationOption.Internal"/> option.
        /// Use this method to deserialize internal data. Using isolated settings that can't be changed from user code.
        /// </summary>
        public static T Deserialize<T>([CanBeNull] string value)
        {
            if (value == null) return default(T);
            return Deserialize<T>(value, SerializationOption.Internal);
        }

        /// <summary>
        /// Deserializes data with specified option. 
        /// Use <see cref="SerializationOption.Internal"/> to deserialize internal data.
        /// Use <see cref="SerializationOption.TypedInternal"/> if deserializable internal data has type names information.
        /// Use <see cref="SerializationOption.User"/> to deserialize user data like arguments and parameters, 
        /// configurable via <see cref="SetUserSerializerOptions"/>.
        /// </summary>
        public static T Deserialize<T>([CanBeNull] string value, SerializationOption option)
        {
            if (value == null) return default(T);
            return (T) Deserialize(value, typeof(T), option);
        }

        internal static JsonSerializerOptions GetInternalOptions()
        {
            var serializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                MaxDepth = 128,
                PropertyNameCaseInsensitive = true
            };

            serializerOptions.Converters.Add(new TypeJsonConverter());
            serializerOptions.Converters.Add(new TimeSpanJsonConverter());
            serializerOptions.Converters.Add(new TypeMetadataJsonConverterFactory());

            return serializerOptions;
        }

        internal static void SetUserSerializerOptions([CanBeNull] JsonSerializerOptions options)
        {
            Volatile.Write(ref _userSerializerOptions, options == null ? null : WithUserConverters(options));
        }

        private static JsonSerializerOptions GetDefaultUserOptions()
        {
            var serializerOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                MaxDepth = 128,
                PropertyNameCaseInsensitive = true
            };

            AddUserConverters(serializerOptions);

            return serializerOptions;
        }

        private static JsonSerializerOptions GetSerializerOptions(SerializationOption serializationOption)
        {
            switch (serializationOption)
            {
                case SerializationOption.Internal:
                case SerializationOption.TypedInternal: return InternalSerializerOptions.Value;
                case SerializationOption.User: return GetUserSerializerOptions();
                default: throw new ArgumentOutOfRangeException(nameof(serializationOption), serializationOption, null);
            }
        }

        private static JsonSerializerOptions GetUserSerializerOptions()
        {
            return Volatile.Read(ref _userSerializerOptions) ?? DefaultUserSerializerOptions.Value;
        }

        private static bool UsesTypeMetadata(JsonSerializerOptions options, Type type)
        {
            foreach (var converter in options.Converters)
            {
                if (converter is TypeMetadataJsonConverterFactory && converter.CanConvert(type))
                {
                    return true;
                }
            }

            return false;
        }

        private static string SerializeWithTypeMetadata(object value, JsonSerializerOptions options)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
                {
                    Encoder = options.Encoder,
                    Indented = options.WriteIndented
                }))
                {
                    WriteValueWithTypeMetadata(writer, value, options);
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static void WriteValueWithTypeMetadata(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var runtimeType = value.GetType();

            if (!ShouldWriteMetadata(runtimeType))
            {
                JsonSerializer.Serialize(writer, value, runtimeType, options);
                return;
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, runtimeType, options);
            using var document = JsonDocument.Parse(bytes);
            var element = document.RootElement;

            if (element.ValueKind != JsonValueKind.Object)
            {
                element.WriteTo(writer);
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(TypePropertyName, TypeHelper.SimpleAssemblyTypeSerializer(runtimeType));

            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(TypePropertyName)) continue;
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        private static bool ShouldWriteMetadata(Type runtimeType)
        {
            var typeInfo = runtimeType.GetTypeInfo();
            return !typeInfo.IsPrimitive &&
                   runtimeType != typeof(string) &&
                   runtimeType != typeof(decimal) &&
                   runtimeType != typeof(DateTime) &&
                   runtimeType != typeof(DateTimeOffset) &&
                   runtimeType != typeof(Guid);
        }

        private sealed class TypeJsonConverter : JsonConverter<Type>
        {
            public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;

                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException($"Expected a JSON string when reading {nameof(Type)}.");
                }

                var typeName = reader.GetString();
                return typeName == null ? null : TypeHelper.CurrentTypeResolver(typeName);
            }

            public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStringValue(TypeHelper.CurrentTypeSerializer(value));
            }
        }

        private sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException($"Expected a JSON string when reading {nameof(TimeSpan)}.");
                }

                return TimeSpan.Parse(reader.GetString(), CultureInfo.InvariantCulture);
            }

            public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString("c", CultureInfo.InvariantCulture));
            }

        }

        private sealed class TimeSpanDictionaryJsonConverterFactory : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert.GetTypeInfo().IsGenericType &&
                       typeToConvert.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                       typeToConvert.GetGenericArguments()[0] == typeof(TimeSpan);
            }

            public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            {
                return (JsonConverter)Activator.CreateInstance(
                    typeof(TimeSpanDictionaryJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[1]));
            }
        }

        private sealed class TimeSpanDictionaryJsonConverter<TValue> : JsonConverter<Dictionary<TimeSpan, TValue>>
        {
            public override Dictionary<TimeSpan, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException("Expected a JSON object when reading a TimeSpan-keyed dictionary.");
                }

                var result = new Dictionary<TimeSpan, TValue>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return result;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException("Expected a JSON property name when reading a TimeSpan-keyed dictionary.");
                    }

                    var key = TimeSpan.Parse(reader.GetString(), CultureInfo.InvariantCulture);
                    if (!reader.Read())
                    {
                        throw new JsonException("Expected a JSON value when reading a TimeSpan-keyed dictionary.");
                    }

                    var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                    result.Add(key, value);
                }

                throw new JsonException("Expected the end of a JSON object when reading a TimeSpan-keyed dictionary.");
            }

            public override void Write(Utf8JsonWriter writer, Dictionary<TimeSpan, TValue> value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStartObject();
                foreach (var item in value)
                {
                    writer.WritePropertyName(item.Key.ToString("c", CultureInfo.InvariantCulture));
                    JsonSerializer.Serialize(writer, item.Value, options);
                }
                writer.WriteEndObject();
            }
        }

        private sealed class InferredObjectJsonConverter : JsonConverter<object>
        {
            public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Null:
                        return null;
                    case JsonTokenType.String:
                        return reader.GetString();
                    case JsonTokenType.True:
                        return true;
                    case JsonTokenType.False:
                        return false;
                    case JsonTokenType.Number:
                        if (reader.TryGetInt64(out var longValue)) return longValue;
                        if (reader.TryGetDecimal(out var decimalValue)) return decimalValue;
                        return reader.GetDouble();
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        using (var document = JsonDocument.ParseValue(ref reader))
                        {
                            return document.RootElement.Clone();
                        }
                    default:
                        throw new JsonException($"Unexpected token '{reader.TokenType}' when reading object.");
                }
            }

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }

        private sealed class CultureInfoJsonConverter : JsonConverter<CultureInfo>
        {
            public override CultureInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException($"Expected a JSON string when reading {nameof(CultureInfo)}.");
                }

                var cultureName = reader.GetString();
                return String.IsNullOrEmpty(cultureName) ? CultureInfo.InvariantCulture : new CultureInfo(cultureName);
            }

            public override void Write(Utf8JsonWriter writer, CultureInfo value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStringValue(value.Name);
            }
        }

        private sealed class TypeMetadataJsonConverterFactory : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                var typeInfo = typeToConvert.GetTypeInfo();
                return typeToConvert == typeof(object) || typeInfo.IsInterface || typeInfo.IsAbstract;
            }

            public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            {
                return (JsonConverter)Activator.CreateInstance(
                    typeof(TypeMetadataJsonConverter<>).MakeGenericType(typeToConvert));
            }
        }

        private sealed class TypeMetadataJsonConverter<T> : JsonConverter<T>
        {
            private static readonly Type DeclaredType = typeof(T);

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                var element = document.RootElement;

                if (element.ValueKind == JsonValueKind.Null)
                {
                    return default;
                }

                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty(TypePropertyName, out var typeProperty))
                {
                    var typeName = typeProperty.GetString();
                    if (String.IsNullOrEmpty(typeName))
                    {
                        throw new JsonException($"The '{TypePropertyName}' metadata property can't be empty.");
                    }

                    var actualType = TypeHelper.CurrentTypeResolver(typeName);
                    if (DeclaredType != typeof(object) &&
                        !DeclaredType.GetTypeInfo().IsAssignableFrom(actualType.GetTypeInfo()))
                    {
                        throw new JsonException(
                            $"The type '{actualType}' is not assignable to '{DeclaredType}'.");
                    }

                    return (T)JsonSerializer.Deserialize(element.GetRawText(), actualType, options);
                }

                if (DeclaredType == typeof(object))
                {
                    return (T)(object)JsonSerializer.Deserialize<JsonElement>(element.GetRawText(), options);
                }

                throw new JsonException($"The '{TypePropertyName}' metadata property is required for '{DeclaredType}'.");
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                WriteValueWithTypeMetadata(writer, value, options);
            }
        }

        private static JsonSerializerOptions WithUserConverters(JsonSerializerOptions options)
        {
            if (HasConverterFor<Type>(options) &&
                HasConverterFor<TimeSpan>(options) &&
                HasConverterFor<Dictionary<TimeSpan, object>>(options) &&
                HasConverterFor<object>(options) &&
                HasConverterFor<CultureInfo>(options))
            {
                return options;
            }

            var serializerOptions = new JsonSerializerOptions(options);
            AddUserConverters(serializerOptions);
            return serializerOptions;
        }

        private static void AddUserConverters(JsonSerializerOptions options)
        {
            if (!HasConverterFor<Type>(options))
            {
                options.Converters.Add(new TypeJsonConverter());
            }

            if (!HasConverterFor<TimeSpan>(options))
            {
                options.Converters.Add(new TimeSpanJsonConverter());
            }

            if (!HasConverterFor<Dictionary<TimeSpan, object>>(options))
            {
                options.Converters.Add(new TimeSpanDictionaryJsonConverterFactory());
            }

            if (!HasConverterFor<object>(options))
            {
                options.Converters.Add(new InferredObjectJsonConverter());
            }

            if (!HasConverterFor<CultureInfo>(options))
            {
                options.Converters.Add(new CultureInfoJsonConverter());
            }
        }

        private static bool HasConverterFor<T>(JsonSerializerOptions options)
        {
            var type = typeof(T);
            foreach (var converter in options.Converters)
            {
                if (converter.CanConvert(type)) return true;
            }

            return false;
        }
    }
}
