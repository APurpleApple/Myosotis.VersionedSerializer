using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Myosotis.VersionedSerializer.Versionizer;

namespace Myosotis.VersionedSerializer
{
    public class Version
    {
        internal int number = 0;
        internal List<UpdateAction> actions = new();
        internal Dictionary<int, FieldInfo> signature = new();

        internal Version(Version? previous)
        {
            if (previous != null)
            {
                foreach (var item in previous.signature)
                {
                    signature.Add(item.Key, item.Value);
                }
            }
        }

        public void AddField<T>(string name)
        {
            Type type = typeof(T);
            signature.Add(name.GetHashCode(), new FieldInfo() { type = type, name = name});
            actions.Add(new AddFieldAction(name, type, number));
        }

        public void RemoveField(string name)
        {
            signature.Remove(name.GetHashCode());
            actions.Add(new RemoveFieldAction(name));
        }

        public void RenameField(string oldName, string newName) 
        {
            int newHash = newName.GetHashCode();
            int oldHash = oldName.GetHashCode();
            signature[newHash] = signature[oldHash];
            signature.Remove(oldHash);
            actions.Add(new RenameFieldAction(oldName, newName));
        }

        /// <summary>
        /// Allow manipulation of the object's values. Do not reference the version in there !
        /// </summary>
        /// <param name="transformation"></param>
        public void Transform(Action<ITransformer, ObjectID> transformation)
        {
            actions.Add(new TransformAction(transformation));
        }

        /// <summary>
        /// Change a field's type to a new one. A method can be provided in order to translate the old field (1st FieldID) to the new one (2nd FieldID).
        /// </summary>
        /// <typeparam name="T">The old type</typeparam>
        /// <typeparam name="U">The new type</typeparam>
        /// <param name="name">The field's name</param>
        /// <param name="transformation">The transformation method</param>
        public void ChangeFieldType<T,U>(string name, Action<ITransformer, FieldID, FieldID>? transformation = null)
        {
            Type newType = typeof(U);
            actions.Add(new ChangeFieldTypeAction(transformation, name, newType, number));
            signature[name.GetHashCode()] = new FieldInfo() { type = newType, name = name };
        }

        internal void Update(Versionizer converter, ObjectID id)
        {
            foreach (var action in actions)
            {
                action.Do(converter, id);
            }
        }

        internal abstract class UpdateAction
        {
            internal abstract void Do(Versionizer converter, ObjectID id);
        }

        internal class TransformAction : UpdateAction
        {
            Action<ITransformer, ObjectID> transformation;

            internal TransformAction(Action<ITransformer, ObjectID> transformation)
            {
                this.transformation = transformation;
            }

            internal override void Do(Versionizer converter, ObjectID id)
            {
                converter.context = Context.transforming;
                transformation.Invoke(converter, id);
                converter.context = Context.versioning;
            }
        }

        internal class ChangeFieldTypeAction : UpdateAction
        {
            Action<ITransformer, FieldID, FieldID>? transformation;
            string name;
            Type newType;
            int version;

            internal ChangeFieldTypeAction(Action<ITransformer, FieldID, FieldID>? transformation, string name, Type newType, int version)
            {
                this.transformation = transformation;
                this.name = name;
                this.newType = newType;
                this.version = version;
            }

            internal override void Do(Versionizer converter, ObjectID id)
            {
                FieldID oldFieldID = new FieldID(converter.Internal_FindField(name, id.id));
                SerializedField oldField = converter.objectFields[oldFieldID.id];

                SerializedTypes sType = Versionizer.Internal_FindSerializedType(newType);
                SerializedField newField = new SerializedField(sType, converter.Internal_RegisterDefaultThingAtVersion(newType, version), oldField.name, oldField.next);
                FieldID newFieldID = new FieldID(converter.objectFields.Add(newField));

                converter.context = Context.transforming;
                transformation?.Invoke(converter, oldFieldID, newFieldID);
                converter.context = Context.versioning;

                converter.objectFields[oldFieldID.id] = newField;
                converter.objectFields.Delete(newFieldID.id);
                converter.Internal_DeleteThing(oldField.type, oldField.index);
            }
        }

        internal class AddFieldAction : UpdateAction
        {
            string name;
            Type type;
            int version;

            internal AddFieldAction(string name, Type type, int version)
            {
                this.name = name;
                this.type = type;
                this.version = version;
            }

            internal override void Do(Versionizer converter, ObjectID id)
            {
                SerializedTypes sType = Versionizer.Internal_FindSerializedType(type);
                converter.Internal_AttachFieldToObject(name, sType, converter.Internal_RegisterDefaultThingAtVersion(type, version), id.id);
            }
        }

        internal class RemoveFieldAction : UpdateAction
        {
            string name;

            internal RemoveFieldAction(string name)
            {
                this.name = name;
            }
            internal override void Do(Versionizer converter, ObjectID id)
            {
                converter.Internal_RemoveObjectField(name, id.id);
            }
        }

        internal class RenameFieldAction : UpdateAction
        {
            string newName;
            string oldName;

            internal RenameFieldAction(string oldName, string newName)
            {
                this.newName = newName;
                this.oldName = oldName;
            }

            internal override void Do(Versionizer converter, ObjectID id)
            {
                int fieldIndex = converter.Internal_FindField(oldName, id.id);

                SerializedField field = converter.objectFields[fieldIndex];
                converter.Internal_DeleteString(field.name);
                converter.objectFields[fieldIndex] = new SerializedField(field.type, field.index, converter.Internal_RegisterString(newName), field.next);
            }
        }

        internal class FieldInfo()
        {
            internal Type type;
            internal string name;
        }

    }
}
