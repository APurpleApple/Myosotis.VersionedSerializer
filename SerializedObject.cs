using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Myosotis.VersionedSerializer
{
    public class SerializedObject : SerializedItem
    {
        public int version = -1;
        internal Dictionary<string, SerializedItem> fields = [];

        internal SerializedObject()
        {
        }

        public SerializedObject RenameField(string name, string newName)
        {
            if (fields.ContainsKey(newName))
            {
                VersionedConvert.Internal_Log($"Renaming issue: new name \"{newName}\" already exists, overriding previous value", LogPriority.warning);
            }
            if (fields.ContainsKey(name))
            {
                fields[newName] = fields[name];
                fields.Remove(name);
            }
            return this;
        }

        public SerializedObject SetField<T>(string name, T value)
        {
            Type valueType = value.GetType();

            if (!fields.ContainsKey(name))
            {
                VersionedConvert.Internal_Log($"Setting unknown field \"{name}\" in serialized object of type {cachedType} at version {version}: It should have been Added first.", LogPriority.warning);
            }

            SerializedItem item = VersionedConvert.Internal_ConvertToItem(value);
            if (item != null)
            {
                fields[name] = item;
            }
            else
            {
                VersionedConvert.Internal_Log($"Cannot set field of type {valueType}: Type not supported", LogPriority.error);
            }

            return this;
        }

        public SerializedObject AddField<T>(string name, T value)
        {
            fields.Add(name, VersionedConvert.Internal_ConvertToItem(value));
            return this;
        }

        public SerializedObject RemoveField(string name)
        {
            if (!fields.Remove(name))
            {
                VersionedConvert.Internal_Log($"Removed field \"{name}\" doesn't exist in serialized object of type {cachedType} at version {version}: It should have been Added first.", LogPriority.warning);
            }
            return this;
        }

        public SerializedObject TransformField<T, U>(string name, Func<U, T> transformer)
        {
            SetField(name, transformer.Invoke(GetField<U>(name)));
            return this;
        }

        public SerializedObject GetField<T>(string name, out T value)
        {
            value = GetField<T>(name);
            return this;
        }

        public T GetField<T>(string name)
        {
            if (fields.TryGetValue(name, out var item))
            {
                return item.Internal_Get<T>();
            }

            VersionedConvert.Internal_Log($"Field with name {name} doesn't exist in serialized object of type {cachedType} at version {version}", LogPriority.error);
            return default;
        }

        public SerializedObject GetField(string name, out SerializedObject value)
        {
            value = GetField<SerializedObject>(name);
            return this;
        }

        internal override T Internal_Get<T>()
        {
            return (T)Internal_Get(typeof(T));
        }

        internal override object Internal_Get(Type type)
        {
            if (type == typeof(SerializedObject))
            {
                return this;
            }
            return VersionedConvert.Deserialize(type, this);
        }

        internal override void Internal_ToField(FieldInfo field, object obj)
        {
            field.SetValue(obj, VersionedConvert.Deserialize(field.FieldType, this));
        }

        internal override SerializedItem Internal_FromField(FieldInfo field, object obj)
        {
            return VersionedConvert.Serialize(field.GetValue(obj));
        }

        internal override void Internal_Update(Type expectedType, int targetVersion)
        {
            Type type = this.cachedType ?? expectedType;
            VersionedSerializer serializer = VersionedConvert.Internal_GetSerializer(type);
            foreach (var item in fields)
            {
                item.Value.Internal_Update(serializer.Internal_GetObjectFieldTypeAtVersion(item.Key, targetVersion), targetVersion);
            }
            if (serializer.versions.TryGetValue(targetVersion, out Version version))
            {
                version.transformer(this);
            }
            this.version = targetVersion;
        }

        internal override int Internal_GetByteSize()
        {
            int size = 1; // serial type
            size += 2; // item count (ushort)

            foreach (var item in fields)
            {
                size += ByteWriter.GetStringByteCount(item.Key);
                size += item.Value.Internal_GetByteSize();
            }

            return size;
        }

        internal override void Internal_WriteBytes(ByteWriter writer)
        {
            writer.Write((byte)SerializationType.@object);
            writer.Write((ushort)fields.Count);

            foreach (var item in fields)
            {
                writer.Write(item.Key);
                item.Value.Internal_WriteBytes(writer);
            }
        }

        internal override void Internal_ReadBytes(ByteReader reader, int version)
        {
            this.version = version;
            ushort fieldCount = reader.ReadUInt16();
            for (int i = 0; i < fieldCount; i++)
            {
                string key = reader.ReadString();
                SerializationType type = (SerializationType)reader.ReadByte();
                SerializedItem item = VersionedConvert.Internal_CreateItemFromEnum(type);
                item.Internal_ReadBytes(reader, version);
                fields.Add(key, item);
            }
        }

        static int nextUuid = 0;
        int uuid = nextUuid++;
        internal override void Internal_ReadJson(Utf8JsonReader reader, int version)
        {
            this.version = version;
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    //reader.Read();
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string key = reader.GetString();
                        SerializedItem item = VersionedConvert.Internal_CreateItemFromJson(reader, version);
                        reader.Skip();
                        Console.WriteLine($"JSONREAD: Adding field {key} to object {uuid}");
                        fields.Add(key, item);
                    }
                    VersionedConvert.ReadNext(ref reader, "object " + uuid);
                }
            }
            else
            {
                VersionedConvert.Internal_Log($"JSON error: object doesn't start, TokenType: {reader.TokenType}", LogPriority.error);
            }
        }

        internal override void Internal_WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var item in fields)
            {
                Console.WriteLine("writing json key: " + item.Key);
                writer.WritePropertyName(item.Key);
                item.Value.Internal_WriteJson(writer);
            }
            writer.WriteEndObject();
        }

        public override int GetHashCode()
        {
            int hash = cachedType?.GetHashCode() ?? 0;
            foreach (var item in fields)
            {
                hash += item.Key.GetHashCode() << item.Value.GetHashCode();
            }
            return hash;
        }

        //public byte[] GetBytes()
        //{
        //    List<byte> bytes = [];
        //    bytes.AddRange(BitConverter.GetBytes(version));
        //
        //    bytes.AddRange(BitConverter.GetBytes(valueFields.Count));
        //    foreach (var item in valueFields)
        //    {
        //        var keyBytes = Encoding.UTF8.GetBytes(item.Key);
        //        bytes.AddRange(BitConverter.GetBytes(keyBytes.Length));
        //        bytes.AddRange(keyBytes);
        //        bytes.AddRange(BitConverter.GetBytes(item.Value.Length));
        //        bytes.AddRange(item.Value);
        //    }
        //
        //    bytes.AddRange(BitConverter.GetBytes(objectFields.Count));
        //    foreach (var item in objectFields)
        //    {
        //        var keyBytes = Encoding.UTF8.GetBytes(item.Key);
        //        bytes.AddRange(BitConverter.GetBytes(keyBytes.Length));
        //        bytes.AddRange(keyBytes);
        //        var objectBytes = item.Value.GetBytes();
        //        bytes.AddRange(BitConverter.GetBytes(objectBytes.Length));
        //        bytes.AddRange(objectBytes);
        //    }
        //
        //    return bytes.ToArray();
        //}
        //
        //public static SerializedObject FromBytes(ReadOnlySpan<byte> bytes)
        //{
        //    SerializedObject result = new SerializedObject();
        //    int advance = 0;
        //    result.version = BitConverter.ToUInt32(bytes.Slice(advance,4));
        //    advance += 4;
        //
        //    int valueFieldsCount = BitConverter.ToInt32(bytes.Slice(advance, 4));
        //    advance += 4;
        //    for (int i = 0; i < valueFieldsCount; i++)
        //    {
        //        int keyLength = BitConverter.ToInt32(bytes.Slice(advance, 4));
        //        advance += 4;
        //        bytes.
        //    }
        //
        //    int objectFieldsCount = BitConverter.ToInt32(bytes.Slice(advance, 4));
        //    advance += 4;
        //    for (int i = 0; i < objectFieldsCount; i++)
        //    {
        //
        //    }
        //}
    }
}
