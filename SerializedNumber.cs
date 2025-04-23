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
    public class SerializedNumber : SerializedItem
    {
        internal decimal value;

        internal SerializedNumber(int v)
        {
            value = v;
            cachedType = typeof(int); 
        }
        internal SerializedNumber(float v)
        {
            value = (decimal)v;
            cachedType = typeof(float);
        }
        internal SerializedNumber(double v)
        {
            value = (decimal)v;
            cachedType = typeof(double);
        }
        internal SerializedNumber(uint v)
        {
            value = (decimal)v;
            cachedType = typeof(uint);
        }
        internal SerializedNumber(short v)
        {
            value = (decimal)v;
            cachedType = typeof(short);
        }
        internal SerializedNumber(ushort v)
        {
            value = (decimal)v;
            cachedType = typeof(ushort);
        }
        internal SerializedNumber(long v)
        {
            value = (decimal)v;
            cachedType = typeof(long);
        }
        internal SerializedNumber(ulong v)
        {
            value = (decimal)v;
            cachedType = typeof(ulong);
        }
        internal SerializedNumber(byte v)
        {
            value = (decimal)v;
            cachedType = typeof(byte);
        }
        internal SerializedNumber(decimal v)
        {
            value = v;
            cachedType = typeof(decimal);
        }

        internal SerializedNumber()
        {

        }

        internal override SerializedItem Internal_FromField(FieldInfo field, object obj)
        {
            if (field.FieldType == typeof(decimal)) value = (decimal)field.GetValue(obj);
            else if (field.FieldType == typeof(int)) value = (decimal)(int)field.GetValue(obj);
            else if (field.FieldType == typeof(uint)) value = (decimal)(uint)field.GetValue(obj);
            else if (field.FieldType == typeof(short)) value = (decimal)(short)field.GetValue(obj);
            else if (field.FieldType == typeof(ushort)) value = (decimal)(ushort)field.GetValue(obj);
            else if (field.FieldType == typeof(long)) value = (decimal)(long)field.GetValue(obj);
            else if (field.FieldType == typeof(ulong)) value = (decimal)(ulong)field.GetValue(obj);
            else if (field.FieldType == typeof(float)) value = (decimal)(float)field.GetValue(obj);
            else if (field.FieldType == typeof(double)) value = (decimal)(double)field.GetValue(obj);
            else if (field.FieldType == typeof(byte)) value = (decimal)(byte)field.GetValue(obj);
            else if (field.FieldType.IsEnum)
            {
                value = (decimal)(int)field.GetValue(obj);
            }
            else
            {
                VersionedConvert.Internal_Log($"Cannot fetch number from field {field.Name} of object {obj.GetType()}: unsupported type", LogPriority.warning);
            }
            return this;
        }

        internal override void Internal_ToField(FieldInfo field, object obj)
        {
            if (field.FieldType == typeof(decimal)) field.SetValue(obj, value);
            else if (field.FieldType == typeof(int)) field.SetValue(obj, (int)value);
            else if (field.FieldType == typeof(uint)) field.SetValue(obj, (uint)value);
            else if (field.FieldType == typeof(short)) field.SetValue(obj, (short)value);
            else if (field.FieldType == typeof(ushort)) field.SetValue(obj, (ushort)value);
            else if (field.FieldType == typeof(long)) field.SetValue(obj, (long)value);
            else if (field.FieldType == typeof(ulong)) field.SetValue(obj, (ulong)value);
            else if (field.FieldType == typeof(float)) field.SetValue(obj, (float)value);
            else if (field.FieldType == typeof(double)) field.SetValue(obj, (double)value);
            else if (field.FieldType == typeof(byte)) field.SetValue(obj, Internal_Get(field.FieldType));
            else if (field.FieldType.IsEnum)
            {
                value = (decimal)(int)field.GetValue(obj);
            }
            else
            {
                VersionedConvert.Internal_Log($"Cannot assign number to field {field.Name} of object {obj.GetType()}: unsupported type", LogPriority.warning);
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
            if (type == typeof(decimal)) return value;
            else if (type == typeof(int)) return (int)value;
            else if (type == typeof(uint)) return (uint)value;
            else if (type == typeof(short)) return (short)value;
            else if (type == typeof(ushort)) return (ushort)value;
            else if (type == typeof(long)) return (ulong)value;
            else if (type == typeof(float)) return (float)value;
            else if (type == typeof(double)) return (double)value;
            else if (type == typeof(byte)) return (byte)value;
            else if (type.IsEnum)
            {
                return Enum.ToObject(type, (int)value);
            }

                VersionedConvert.Internal_Log($"Cannot convert {type} to number", LogPriority.error);
            return value;
        }

        internal override int Internal_GetByteSize()
        {
            return 17;
        }

        internal override void Internal_WriteBytes(ByteWriter writer)
        {
            writer.Write((byte)SerializationType.number);
            writer.Write(value);
        }

        internal override void Internal_ReadBytes(ByteReader reader, int version)
        {
            value = reader.ReadDecimal();
        }

        internal override void Internal_ReadJson(Utf8JsonReader reader, int version)
        {
            value = reader.GetDecimal();
        }

        internal override void Internal_WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteNumberValue(value);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
    }
}
