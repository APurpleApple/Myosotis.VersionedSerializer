using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    public abstract class SerializedItem
    {
        internal Type cachedType;
        internal abstract void Internal_ToField(FieldInfo field, object obj);
        internal abstract SerializedItem Internal_FromField(FieldInfo field, object obj);
        internal abstract void Internal_Update(Type expectedType, int targetVersion);
        internal abstract T Internal_Get<T>();
        internal abstract object Internal_Get(Type type);
        internal abstract int Internal_GetByteSize();
        internal abstract void Internal_WriteBytes(ByteWriter writer);
        internal abstract void Internal_ReadBytes(ByteReader reader, int version);
        internal abstract void Internal_WriteJson(Utf8JsonWriter writer);
        internal abstract void Internal_ReadJson(Utf8JsonReader reader, int version);
    }
}
