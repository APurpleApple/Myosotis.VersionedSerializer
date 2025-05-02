using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Myosotis.VersionedSerializer
{
    public static class VersionedConvert
    {
        internal static int latestVersion = 0;
        public static int LatestVersion => latestVersion;

        static Dictionary<Type, VersionedSerializer> serializers = new();

        static Action<string, LogPriority> logFunc;
        static bool throwOnError = true;

        public static T DeserializeFromJson<T>(ReadOnlySpan<byte> json)
        {
            return Deserialize<T>(FromJson(json));
        }
        
        public static ReadOnlySpan<byte> SerializeToJson(object obj)
        {
            return ToJson(Serialize(obj));
        }

        internal static JsonWriterOptions JsonWriterOptions = new JsonWriterOptions() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        internal static JsonReaderOptions JsonReaderOptions = new JsonReaderOptions() { CommentHandling = JsonCommentHandling.Skip };


        public static ReadOnlySpan<byte> ToJson(SerializedObject obj)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, JsonWriterOptions);

            writer.WriteStartObject();
            writer.WriteNumber("version", obj.version);
            writer.WritePropertyName("data");
            obj.Internal_WriteJson(writer);
            writer.WriteEndObject();

            writer.Flush();
            return stream.ToArray().AsSpan();
        }
        
        public static SerializedObject FromJson(ReadOnlySpan<byte> json)
        {
            var reader = new Utf8JsonReader(json);

            //while (ReadNext(ref reader, "analysis"))
            //{
            //
            //}
            //reader = new Utf8JsonReader(json);

            //reader.Read(); // object start
            //reader.Read(); // property name
            //reader.Read(); // version number
            reader.Read();
            reader.Read();
            reader.Read();
            int version = reader.GetInt32();

            reader.Read();
            //reader.Read(); // property name

            SerializedObject obj = new SerializedObject();
            //reader.Read();
            reader.Read();
            obj.Internal_ReadJson(reader, version);

            return obj;
        }

        public static T DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes)
        {
            return Deserialize<T>(FromBytes(bytes));
        }

        public static SerializedObject FromBytes(ReadOnlySpan<byte> bytes)
        {
            ByteReader reader = new ByteReader(bytes);
            SerializedObject obj = new SerializedObject();
            int version = reader.ReadInt32();
            reader.ReadByte();
            obj.Internal_ReadBytes(reader, version);
            return obj;
        }

        public static ByteWriter SerializeToBytes(object obj)
        {
            return ToBytes(Serialize(obj));
        }

        public static ByteWriter ToBytes(SerializedObject obj)
        {
            int size = obj.Internal_GetByteSize();
            ByteWriter writer = new ByteWriter((uint)size);
            writer.Write(10);
            obj.Internal_WriteBytes(writer);
            return writer;
        }

        public static void SetLogOutputFunction(Action<string, LogPriority> func)
        {
            logFunc = func;
        }

        internal static void Internal_Log(string message, LogPriority priority)
        {
            logFunc?.Invoke(message, priority);
            if (priority == LogPriority.error && throwOnError)
            {
                throw new Exception(message);
            }
        }
        public static void ThrowOnError(bool throwOnError)
        {
            VersionedConvert.throwOnError = throwOnError;
        }

        public static SerializedObject Serialize(object thing)
        {
            var thingType = thing.GetType();
            var serializer = Internal_GetSerializer(thingType);

            SerializedObject data = new SerializedObject();
            data.cachedType = thingType;
            serializer.Serialize(thing, data);
            return data;
        }

        public static T Deserialize<T>(SerializedObject data)
        {
            return (T)Deserialize(typeof(T), data);
        }

        public static object Deserialize(Type type, SerializedObject data)
        {
            var serializer = Internal_GetSerializer(type);

            Update(type, LatestVersion, data);
            return serializer.Deserialize(data, type);
        }

        internal static VersionedSerializer Internal_GetSerializer(Type type)
        {
            if (!serializers.TryGetValue(type, out var serializer))
            {
                var attribute = type.GetCustomAttribute<VersionedSerializerAttribute>();
                if (attribute == null)
                {
                    SetSerializer(type, typeof(VersionedSerializer), true);
                }
                else
                {
                    SetSerializer(type, attribute.serializer, false);
                }
            }
            return serializers[type];
        }

        internal static SerializedItem Internal_CreateItemFromEnum(SerializationType type)
        {
            switch (type)
            {
                case SerializationType.number:
                    return new SerializedNumber(0);
                case SerializationType.@string:
                    return new SerializedString("");
                case SerializationType.@object:
                    return new SerializedObject();
                case SerializationType.collection:
                    return new SerializedCollection(null);
                case SerializationType.dictionary:
                    return new SerializedDictionary();
                case SerializationType.@bool:
                    return new SerializedBool(false);
            }

            return null;
        }

        internal static SerializedItem Internal_CreateItemFromJson(Utf8JsonReader reader, int version)
        {
            Utf8JsonReader itemReader = reader;
            SerializedItem item = null;
            switch (itemReader.TokenType)
            {
                case JsonTokenType.StartObject:
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "IsDictionary")
                    {
                        item = new SerializedDictionary();
                    }
                    else
                    {
                        item = new SerializedObject();
                    }
                    break;
                case JsonTokenType.StartArray:
                    item = new SerializedCollection(null);
                    break;
                case JsonTokenType.String:
                    item = new SerializedString("");
                    break;
                case JsonTokenType.Number:
                    item = new SerializedNumber(0);
                    break;
                case JsonTokenType.True:
                    item = new SerializedBool(true);
                    break;
                case JsonTokenType.False:
                    item = new SerializedBool(false);
                    break;
            }

            if (item == null)
            {
                Internal_Log($"Couldn't create serialized item from json {reader.TokenType}", LogPriority.error);
            }

            item?.Internal_ReadJson(itemReader, version);
            return item ?? new SerializedObject();
        }

        internal static SerializedItem Internal_CreateItemFromJsonPropertyName(string property, int version)
        {
            Console.WriteLine(property);

            if (Char.IsLetter(property[0]))
            {
                property = $"\"{property}\"";
            }

            Utf8JsonReader reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(property), JsonReaderOptions);
            reader.Read();
            return Internal_CreateItemFromJson(reader, version);
        }

        internal static ReadOnlySpan<byte> Internal_ToJsonPropertyName(SerializedItem item)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, JsonWriterOptions);

            item.Internal_WriteJson(writer);

            writer.Flush();
            Span<byte> span = stream.ToArray().AsSpan();

            if (item is SerializedString)
            {
                span = span.Slice(1, span.Length - 2);
            }
            return span;
        }

        internal static SerializedItem Internal_ConvertToItem(object obj)
        {
            if (obj is SerializedItem si)
            {
                return si;
            }

            Type type = obj.GetType();

            if (obj is string str)
            {
                return new SerializedString(str);
            }

            if (type.IsPrimitive)
            {
                if (obj is int i) return new SerializedNumber(i);
                if (obj is uint ui) return new SerializedNumber(ui);
                if (obj is short s) return new SerializedNumber(s);
                if (obj is ushort us) return new SerializedNumber(us);
                if (obj is float f) return new SerializedNumber(f);
                if (obj is long l) return new SerializedNumber(l);
                if (obj is ulong ul) return new SerializedNumber(ul);
                if (obj is decimal de) return new SerializedNumber(de);
                if (obj is double d) return new SerializedNumber(d);
                if (obj is byte by) return new SerializedNumber(by);

                if (obj is bool b) return new SerializedBool(b);
            }

            if (type.IsArray)
            {
                SerializedCollection collection = new SerializedCollection(type.GetElementType());
                collection.Internal_FromArray(obj);
                return collection;
            }

            if (type.IsGenericType)
            {
                Type genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                {
                    Type itemType = type.GenericTypeArguments[0];
                    SerializedCollection collection = new SerializedCollection(itemType);
                    collection.Internal_FromList(obj);
                    return collection;
                }
                else if (genericType == typeof(HashSet<>))
                {
                    Type itemType = type.GenericTypeArguments[0];
                    SerializedCollection collection = new SerializedCollection(itemType);
                    collection.Internal_FromHashSet(obj);
                    return collection;
                }
                else if (genericType == typeof(Dictionary<,>))
                {
                    SerializedDictionary dict = new SerializedDictionary();
                    dict.FromDictionary(obj);
                    return dict;
                }
            }

            if (type.IsEnum)
            {
                return new SerializedNumber((int)obj);
            }

            if ((type.IsClass || type.IsValueType) && !type.IsEnum)
            {
                Internal_Log($"Attempt to use type {type} directly. Not using a SerializedObject may cause versioning issues.", LogPriority.warning);
                return Serialize(obj);
            }

            Internal_Log($"Type {type} is not supported", LogPriority.error);
            return null;
        }

        public static void SetSerializer(Type type, Type serializerType, bool isDefault)
        {
            VersionedSerializer serializer = (isDefault ? Activator.CreateInstance(serializerType, [type]) : Activator.CreateInstance(serializerType)) as VersionedSerializer;
            if (serializer == null)
            {
                Internal_Log($"Type {serializerType} doesn't inherit from VersionedSerializer", LogPriority.error);
                return;
            }
            serializers[type] = serializer;
            serializer.RegisterVersionSignatures();
        }
        public static void SetSerializer<T>(Type serializerType)
        {
            SetSerializer(typeof(T), serializerType, serializerType == typeof(VersionedSerializer));
        }

        public static SerializedObject GetUninitialized<T>(int version)
        {
            return GetUninitialized(typeof(T), version);
        }

        public static SerializedObject GetUninitialized(Type t, int version)
        {
            var serializer = Internal_GetSerializer(t);

            SerializedObject result = new();
            result.cachedType = t;
            Update(t, version, result);

            return result;
        }

        public static void Update(Type t, int targetVersion, SerializedObject serializedObject)
        {
            var serializer = Internal_GetSerializer(t);
            for (int i = serializedObject.version+1; i <= targetVersion; i++)
            {
                foreach (var item in serializedObject.fields)
                {
                    item.Value.Internal_Update(item.Value.cachedType ?? serializer.Internal_GetObjectFieldTypeAtVersion(item.Key, targetVersion), i);
                }

                //foreach (var item in serializedObject.objectFields)
                //{
                //    Update(item.Value.type ?? serializer.GetObjectFieldTypeAtVersion(item.Key, i), targetVersion, item.Value);
                //}
                if (serializer.versions.ContainsKey(i))
                {
                    serializer.versions[i].transformer(serializedObject);
                }
            }
            serializedObject.version = targetVersion;
        }

        public static void Update<T>(int targetVersion, SerializedObject serializedObject)
        {
            Update(typeof(T), targetVersion, serializedObject);
        }

        //public static object ProcessSerializationType(Type type, dynamic value, out SerializationType serializationType)
        //{
        //    if (type.IsPrimitive)
        //    {
        //        serializationType = SerializationType.primitive;
        //        return value;
        //    }
        //    if (type == typeof(SerializedObject))
        //    {
        //        serializationType = SerializationType.@object;
        //        return value;
        //    }
        //    if (type.IsGenericType)
        //    {
        //        if (type.GetGenericTypeDefinition() == typeof(List<>))
        //        {
        //            serializationType = SerializationType.collection;
        //            return value.ToArray();
        //        }
        //        if (type.IsArray)
        //        {
        //            serializationType = SerializationType.collection;
        //            return value;
        //        }
        //
        //    }
        //
        //    serializationType = SerializationType.unsupported;
        //    return value;
        //}
    }

}
