using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    public class SerializedDictionary : SerializedItem
    {
        internal Dictionary<int, SerializedItem> values = [];
        internal Dictionary<int, SerializedItem> keys = [];

        public int Count => values.Count;


        public void FromDictionary(dynamic dict)
        {
            Type dictType = dict.GetType();
            if (!dictType.IsGenericType || dictType.GetGenericTypeDefinition() != typeof(Dictionary<,>))
            {
                VersionedConvert.Internal_Log($"Type {dictType} is not a dictionary.", LogPriority.error);
                return;
            }

            cachedType = dictType;
            foreach (var item in dict)
            {
                SerializedItem key = VersionedConvert.Internal_ConvertToItem(item.Key);
                SerializedItem value = VersionedConvert.Internal_ConvertToItem(item.Value);
                int hash = key.GetHashCode();

                keys.Add(hash, key);
                values.Add(hash, value);
            }
        }

        public void TransformValues<T,K,R>(Func<T,K,R> transformer)
        {
            foreach (var item in values)
            {
                values[item.Key] = VersionedConvert.Internal_ConvertToItem(transformer(item.Value.Internal_Get<T>(), keys[item.Key].Internal_Get<K>()));
            }
        }
        public void TransformKeys<T, K, R>(Func<T, K, R> transformer)
        {
            foreach (var item in keys)
            {
                keys[item.Key] = VersionedConvert.Internal_ConvertToItem(transformer(values[item.Key].Internal_Get<T>(), item.Value.Internal_Get<K>()));
            }
        }

        internal override SerializedItem Internal_FromField(FieldInfo field, object obj)
        {
            return new SerializedBool((bool)field.GetValue(obj));
        }

        internal override void Internal_ToField(FieldInfo field, object obj)
        {
            throw new NotImplementedException();
        }

        internal override void Internal_Update(Type expectedType, int targetVersion)
        {
            foreach (var item in values)
            {
                item.Value.Internal_Update(expectedType, targetVersion);
            }
            foreach (var item in keys)
            {
                item.Value.Internal_Update(expectedType, targetVersion);
            }
        }

        internal override T Internal_Get<T>()
        {
            return (T)Internal_Get(typeof(T));
        }

        internal override object Internal_Get(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                dynamic dict = Activator.CreateInstance(type);

                foreach (var item in values)
                {
                    dict.Add(keys[item.Key].Internal_Get(type.GenericTypeArguments[1]), item.Value.Internal_Get(type.GenericTypeArguments[1]));
                }

                return dict;
            }

            VersionedConvert.Internal_Log($"Can't convert serialized dictionary to type {type}", LogPriority.error);
            return null;
        }

        internal override int Internal_GetByteSize()
        {
            int size = 1; // serial type
            size += 4; // count
            foreach (var item in keys)
            {
                size += item.Value.Internal_GetByteSize();
            }
            foreach (var item in values)
            {
                size += item.Value.Internal_GetByteSize();
            }
            return size;
        }

        internal override void Internal_WriteBytes(ByteWriter writer)
        {
            writer.Write((byte)SerializationType.dictionary);
            writer.Write(Count);
            foreach (var item in values)
            {
                keys[item.Key].Internal_WriteBytes(writer);
                item.Value.Internal_WriteBytes(writer);
            }
        }

        internal override void Internal_ReadBytes(ByteReader reader, int version)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                SerializationType keyType = (SerializationType)reader.ReadByte();
                SerializedItem key = VersionedConvert.Internal_CreateItemFromEnum(keyType);
                SerializationType itemType = (SerializationType)reader.ReadByte();
                SerializedItem value = VersionedConvert.Internal_CreateItemFromEnum(itemType);
                int hash = key.GetHashCode();
                values.Add(hash, value);
                keys.Add(hash, key);
            }
        }

        internal override void Internal_ReadJson(Utf8JsonReader reader, int version)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    //reader.Read();
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string keyJson = reader.GetString();
                        SerializedItem key = VersionedConvert.Internal_CreateItemFromJsonPropertyName(keyJson, version);
                        SerializedItem item = VersionedConvert.Internal_CreateItemFromJson(reader, version);
                        reader.Skip();
                        Console.WriteLine($"JSONREAD: Adding item to dictionary");
                        int hash = key.GetHashCode();
                        keys.Add(hash, key);
                        values.Add(hash, item);
                    }
                    VersionedConvert.ReadNext(ref reader, "dictionary");
                }
            }
            else
            {
                VersionedConvert.Internal_Log($"JSON error: dictionary doesn't start, TokenType: {reader.TokenType}", LogPriority.error);

            }
        }

        internal override void Internal_WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteCommentValue("#Dictionary");

            foreach (var item in values)
            {
                writer.WritePropertyName(VersionedConvert.Internal_ToJsonPropertyName(keys[item.Key]));
                item.Value.Internal_WriteJson(writer);
            }

            writer.WriteEndObject();
        }
    }
}
