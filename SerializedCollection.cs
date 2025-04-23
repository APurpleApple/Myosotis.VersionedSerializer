using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Myosotis.VersionedSerializer
{
    public class SerializedCollection : SerializedItem
    {
        internal List<SerializedItem> items = [];
        public int Count => items.Count;

        public SerializedCollection(Type type)
        {
            cachedType = type;
        }

        internal object Internal_ToArray(Type type)
        {
            dynamic array = Array.CreateInstanceFromArrayType(type, Count);
            for (int i = 0; i < Count; i++)
            {
                array[i] = items[i].Internal_Get(type);
            }
            return array;
        }

        internal object Internal_ToList(Type type)
        {
            dynamic list = Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
            foreach (var item in items)
            {
                list.Add(item.Internal_Get(type));
            }
            return list;
        }

        internal object Internal_ToHashSet(Type type)
        {
            dynamic hashSet = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(type));
            foreach (var item in items)
            {
                hashSet.Add(item.Internal_Get(type));
            }
            return hashSet;
        }

        internal void Internal_FromArray(dynamic array)
        {
            items.Clear();
            foreach (var item in array)
            {
                items.Add(VersionedConvert.Internal_ConvertToItem(item));
            }
            cachedType = array.GetType();
        }

        internal void Internal_FromList(dynamic list)
        {
            items.Clear();
            foreach (var item in list)
            {
                items.Add(VersionedConvert.Internal_ConvertToItem(item));
            }
            cachedType = list.GetType();
        }

        internal void Internal_FromHashSet(dynamic hashset)
        {
            items.Clear();
            foreach (var item in hashset)
            {
                items.Add(VersionedConvert.Internal_ConvertToItem(item));
            }
            cachedType = hashset.GetType();
        }

        internal override SerializedItem Internal_FromField(FieldInfo field, object obj)
        {
            if (field.FieldType.IsArray)
            {
                Internal_FromArray(field.GetValue(obj));
            }
            else if (field.FieldType.IsGenericType)
            {
                Type genericType = field.FieldType.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                {
                    Internal_FromList(field.GetValue(obj));
                }
                else if (genericType == typeof(HashSet<>))
                {
                    Internal_FromHashSet(field.GetValue(obj));
                }
            }

            return this;
        }

        internal override void Internal_ToField(FieldInfo field, object obj)
        {
            if (field.FieldType.IsArray)
            {
                field.SetValue(obj, Internal_ToArray(field.FieldType.GetElementType()));
            }
            else if (field.FieldType.IsGenericType)
            {
                Type genericType = field.FieldType.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                {
                    field.SetValue(obj, Internal_ToList(field.FieldType.GenericTypeArguments[0]));
                }
                else if (genericType == typeof(HashSet<>))
                {
                    field.SetValue(obj, Internal_ToHashSet(field.FieldType.GenericTypeArguments[0]));
                }
            }
        }

        internal override void Internal_Update(Type expectedType, int targetVersion)
        {
            Type underlyingType = expectedType;

            if (expectedType.IsArray)
            {
                underlyingType = expectedType.GetElementType();
            }
            else if (expectedType.IsGenericType)
            {
                underlyingType = expectedType.GenericTypeArguments[0];
            }

            foreach (var item in items)
            {
                item.Internal_Update(underlyingType, targetVersion);
            }
        }

        internal override T Internal_Get<T>()
        {
            return (T)Internal_Get(typeof(T));
        }

        public T GetItem<T>(int index)
        {
            return items[index].Internal_Get<T>();
        }

        public void AddItem(object item)
        {
            items.Add(VersionedConvert.Internal_ConvertToItem(item));
        }

        public void RemoveItem(int index)
        {
            items.RemoveAt(index);
        }

        public void SetItem(object item, int index)
        {
            items[index] = VersionedConvert.Internal_ConvertToItem(item);
        }

        public void TransformItems<T,U>(Func<T,U> transformer)
        {
            for (int i = 0; i < Count; i++)
            {
                SetItem(transformer(GetItem<T>(i)), i);
            }
        }

        internal override object Internal_Get(Type type)
        {
            if (type == typeof(SerializedCollection)) return this;
            if (type.IsGenericType)
            {
                Type genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(List<>)) return Internal_ToList(type.GenericTypeArguments[0]);
                if (genericType == typeof(HashSet<>)) return Internal_ToHashSet(type.GenericTypeArguments[0]);
            }
            else if (type.IsArray)
            {
                return Internal_ToArray(type.GetElementType());
            }

            VersionedConvert.Internal_Log($"Can't convert serialized object to type {type}", LogPriority.error);
            return null;
        }

        internal override int Internal_GetByteSize()
        {
            int size = 5;
            foreach (var item in items)
            {
                size += item.Internal_GetByteSize();
            }
            return size;
        }

        internal override void Internal_WriteBytes(ByteWriter writer)
        {
            writer.Write((byte)SerializationType.collection);
            writer.Write(items.Count);
            foreach (var item in items)
            {
                item.Internal_WriteBytes(writer);
            }
        }

        internal override void Internal_ReadBytes(ByteReader reader, int version)
        {
            items.Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                SerializationType stype = (SerializationType)reader.ReadByte();
                SerializedItem item = VersionedConvert.Internal_CreateItemFromEnum(stype);
                item.Internal_ReadBytes(reader, version);
                items.Add(item);
            }
        }

        internal override void Internal_ReadJson(Utf8JsonReader reader, int version)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                VersionedConvert.ReadNext(ref reader, "collection");

                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    SerializedItem item = VersionedConvert.Internal_CreateItemFromJson(reader, version);
                    reader.Skip();
                    items.Add(item);
                    VersionedConvert.ReadNext(ref reader, "collection next item");
                }
            }
            else
            {
                VersionedConvert.Internal_Log($"JSON error: array doesn't start, TokenType: {reader.TokenType}", LogPriority.error);

            }
        }

        internal override void Internal_WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartArray();
            foreach (var item in items)
            {
                item.Internal_WriteJson(writer);
            }
            writer.WriteEndArray();
        }
    }
}
