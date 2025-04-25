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
    public class SerializedString : SerializedItem
    {
        internal string value;

        internal SerializedString(string s)
        {
            value = s;
        }

        internal override SerializedItem Internal_FromField(FieldInfo field, object obj)
        {
            return new SerializedString((string)field.GetValue(obj));
        }

        internal override void Internal_ToField(FieldInfo field, object obj)
        {
            field.SetValue(obj, value);
        }

        internal override void Internal_Update(Type expectedType, int targetVersion)
        {
        }

        internal override T Internal_Get<T>()
        {
            return (T)Internal_Get(typeof(T));
        }

        internal override object Internal_Get(Type type)
        {
            if (type == typeof(string))
            {
                return value;
            }

            VersionedConvert.Internal_Log($"Cannot convert string to {type}", LogPriority.error);
            return default;
        }

        internal override int Internal_GetByteSize()
        {
            int size = 1; // serial type;
            size += ByteWriter.GetStringByteCount(value);
            return size;
        }

        internal override void Internal_WriteBytes(ByteWriter writer)
        {
            writer.Write((byte)SerializationType.@string);
            writer.Write(value);
        }

        internal override void Internal_ReadBytes(ByteReader reader, int version)
        {
            value = reader.ReadString();
        }

        internal override void Internal_ReadJson(Utf8JsonReader reader, int version)
        {
            value = reader.GetString();
        }

        internal override void Internal_WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStringValue(value);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
    }
}
