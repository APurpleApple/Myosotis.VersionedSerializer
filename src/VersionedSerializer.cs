using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Myosotis.VersionedSerializer.Versionizer;

namespace Myosotis.VersionedSerializer
{
    public class VersionedSerializerAttribute(Type serializer) : Attribute
    {
        internal Type serializer = serializer;
    }

    public class VersionedSerializer<T> : VersionedSerializer
    {
        public VersionedSerializer() : base(typeof(T))
        {
        }

        public sealed override void Serialize(object obj, ISerializer serializer, ObjectID id)
        {
            Serialize((T)obj, serializer, id);
        }

        public virtual void Serialize(T obj, ISerializer serializer, ObjectID id)
        {
            base.Serialize(obj, serializer, id);
        }

        public sealed override object Deserialize(ISerializer serializer, Type type, ObjectID id)
        {
            return Deserialize(serializer, id);
        }

        public virtual T Deserialize(ISerializer serializer, ObjectID id)
        {
            return (T)base.Deserialize(serializer, typeof(T), id);
        }
    }

    public class VersionedSerializer
    {
        internal List<Version> versions = new();
        internal Dictionary<int, int> versionMapping = new();
        internal Version? previous;
        internal Type type;
        internal int latestVersion = -1;

        internal Version GetLatestCompatibleVersion(int targetVersion)
        {
            int index = versions.Count / 2;
            int bestVersion = 0;

            int versionCount = versions.Count;
            int maxIteration = (int)Math.Ceiling(Math.Log2(versionCount));

            for (int i = 1; i < maxIteration+1; i++)
            {
                int current = versions[index].number;
                int gap = targetVersion - current;

                if (gap > bestVersion)
                {
                    bestVersion = current;
                    index += versionCount / (2<<i);
                }
                else
                {
                    index -= versionCount / (2<<i);
                }
            }

            return versions[bestVersion];
        }

        internal Type GetFieldTypeAtVersion(int hash, int version)
        {
            return GetLatestCompatibleVersion(version).signature[hash].type;
        }

        public VersionedSerializer(Type type)
        {
            this.type = type;
        }

        public virtual void InitVersionizer()
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            Version version = NewVersion(0);

            foreach (var field in fields)
            {
                version.signature.Add(field.Name.GetHashCode(), new Version.FieldInfo() { type = field.FieldType, name = field.Name});
            }
        }

        public Version NewVersion(int version)
        {
            if (latestVersion >= version) throw new Exception("Cannot register version {version} after version {latestVersion}, please register versions in ascending order.");

            Version v = new Version(previous);
            versions.Add(v);
            versionMapping.Add(version, versions.Count - 1);
            previous = v;
            latestVersion = version;
            return v;
        }

        public virtual void Serialize(object obj, ISerializer serializer, ObjectID id)
        {
            serializer.Serialize(id, obj);
        }

        public virtual object Deserialize(ISerializer serializer, Type type, ObjectID id)
        {
            return serializer.Deserialize(id, type);
        }

    }
}
