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
    public class SerializedBool : SerializedItem
    {
        internal bool value = false;

        public SerializedBool(bool v)
        {
            value = v;
        }

        internal override SerializedItem Internal_FromField(FieldInfo field, object obj)
        {
            return new SerializedBool((bool)field.GetValue(obj));
        }

        internal override void Internal_ToField(FieldInfo field, object obj)
        {
            if (field.FieldType == typeof(bool)) field.SetValue(obj, value);
            else
            {
                VersionedConvert.Internal_Log($"Cannot assign bool to field {field.Name} of object {obj.GetType()}: unsupported type", LogPriority.warning);
            }
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
            if (type == typeof(bool)) return value;

            VersionedConvert.Internal_Log($"Cannot convert {type} to bool", LogPriority.error);
            return value;
        }

        internal override int Internal_GetByteSize()
        {
            return 2;
        }

        internal override void Internal_WriteBytes(ByteWriter writer)
        {
            writer.Write((byte)SerializationType.@bool);
            writer.Write(value);
        }

        internal override void Internal_ReadBytes(ByteReader reader, int version)
        {
            value = reader.ReadBool();
        }

        internal override void Internal_ReadJson(Utf8JsonReader reader, int version)
        {
            value = reader.GetBoolean();
        }

        internal override void Internal_WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteBooleanValue(value);
        }
    }
}
