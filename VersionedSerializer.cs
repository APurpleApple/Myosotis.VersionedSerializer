using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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

        public sealed override void Serialize(object obj, SerializedObject serialized)
        {
            Serialize((T)obj, serialized);
        }

        public virtual void Serialize(T obj, SerializedObject serialized)
        {
            base.Serialize(obj, serialized);
        }

        public sealed override object Deserialize(SerializedObject serialized, Type type)
        {
            return Deserialize(serialized);
        }

        public virtual T Deserialize(SerializedObject serialized)
        {
            return (T)base.Deserialize(serialized, type);
        }
    }

    public class VersionedSerializer
    {
        internal Dictionary<int, Version> versions = [];
        internal int latestVersion = -1;
        internal Type type;

        public VersionedSerializer(Type type)
        {
            this.type = type;
            InitVersionizer();
        }

        internal void RegisterVersionSignatures()
        {
            SerializedObject serializedObject = new SerializedObject();
            foreach (var version in versions)
            {
                VersionedConvert.Update(type, version.Key, serializedObject);
                Dictionary<string, Type> signature = new Dictionary<string, Type>();
                foreach (var field in serializedObject.fields)
                {
                    signature.Add(field.Key, field.Value.cachedType);
                }
                version.Value.signature = signature;
                //Console.WriteLine($"Added signature for version {version.Key} of type {type}");
            }
        }



        public virtual void InitVersionizer()
        {
            RegisterChanges(0, (o) => {
                foreach (var item in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (item.FieldType.IsClass)
                    {
                        o.AddField(item.Name, VersionedConvert.GetUninitialized(item.FieldType, VersionedConvert.latestVersion));
                    }
                    else if (item.FieldType.IsPrimitive)
                    {
                        o.AddField(item.Name, Activator.CreateInstance(item.FieldType));
                    }
                }
            });
        }

        internal Type Internal_GetObjectFieldTypeAtVersion(string key, int version)
        {
            Type result = null;
            foreach (var item in versions)
            {
                if (item.Key > version) break;
                if (item.Value.signature.TryGetValue(key, out Type type))
                {
                    result = type;
                }
            }

            return result;
        }

        public void RegisterChanges(int versionId, Action<SerializedObject> action)
        {
            if (versionId <= latestVersion)
            {
                VersionedConvert.Internal_Log($"Cannot register earlier version {versionId} changes for type {type}. Latest version is {latestVersion}.", LogPriority.error);
                return;
            }
            versions[versionId] = new Version() {transformer = action};
            latestVersion = versionId;
            VersionedConvert.latestVersion = int.Max(versionId, VersionedConvert.latestVersion);
        }

        public virtual void Serialize(object obj, SerializedObject serialized)
        {
            Console.WriteLine($"Serializing {obj.GetType()}");

            VersionedConvert.Update(type, VersionedConvert.LatestVersion, serialized);

            foreach (var item in serialized.fields)
            {
                if (item.Value is SerializedString str)
                {
                    string fieldName = str.Internal_Get<string>();
                    FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
                    if (field != null)
                    {
                        serialized.SetField(fieldName, item.Value.Internal_FromField(field, obj));
                    }
                }
            }

            //foreach (var item in serialized.primitiveFields)
            //{
            //    FieldInfo? field = type.GetField(item.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
            //    if (field != null)
            //    {
            //        serialized.SetField(item.Key, field.GetValue(obj));
            //    }
            //}
            //foreach (var item in serialized.objectFields)
            //{
            //    FieldInfo? field = type.GetField(item.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
            //    if (field != null)
            //    {
            //        serialized.SetField(item.Key, VersionedConvert.Serialize(field.GetValue(obj)));
            //    }
            //}
        }

        public virtual object Deserialize(SerializedObject serialized, Type type)
        {
            object result = Activator.CreateInstance(type);

            foreach (var item in serialized.fields)
            {
                if (item.Value is SerializedString str)
                {
                    string fieldName = str.Internal_Get<string>();
                    FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
                    if (field != null)
                    {
                        item.Value.Internal_ToField(field, result);
                    }
                }
            }

            //foreach (var item in serialized.primitiveFields)
            //{
            //    FieldInfo? field = type.GetField(item.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
            //    if (field != null)
            //    {
            //        field.SetValue(result, item.Value);
            //    }
            //}
            //foreach (var item in serialized.objectFields)
            //{
            //    FieldInfo? field = type.GetField(item.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
            //    if (field != null)
            //    {
            //        Type fieldType = field.FieldType;
            //        field.SetValue(result, VersionedConvert.Deserialize(fieldType, item.Value));
            //    }
            //}
            return result;
        }

    }
}
