using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;

namespace Myosotis.VersionedSerializer
{
    public class Versionizer : ISerializer
    {
        internal NativeArray<SerializedObject> objects = new();
        internal NativeArray<SerializedField> objectFields = new();

        internal NativeArray<SerializedString> strings = new();
        internal NativeArray<SerializedStringChar> stringChars = new();

        internal NativeArray<SerializedCollection> collections = new();
        internal NativeArray<SerializedCollectionElement> collectionElements = new();

        internal NativeArray<SerializedDictionary> dictionaries = new();
        internal NativeArray<SerializedDictionaryEntry> dictionaryEntries = new();

        internal NativeArray<double> numbers = new();
        internal NativeArray<char> chars = new();
        internal NativeArray<bool> bools = new();

        static Dictionary<Type, VersionedSerializer> serializers = new();
        static Versionizer staticConverter = new Versionizer();

        int version = 0;
        int latestVersion = 0;

        internal Context context = Context.versioning;

        internal void Clear()
        {
            objects.Clear();
            objectFields.Clear();
            strings.Clear();
            stringChars.Clear();
            dictionaries.Clear();
            dictionaryEntries.Clear();
            collections.Clear();
            collectionElements.Clear();
            numbers.Clear();
            chars.Clear();
            bools.Clear();
            latestVersion = 0;
            version = 0;
        }

        #region ITransformer
        public ObjectID CreateObject<T>()
        {
            return new ObjectID(Internal_RegisterDefaultObjectAtVersion(typeof(T), version));
        }
        public void SetFieldValue<T>(ObjectID obj, string name, T value)
        {
            Internal_SetObjectFieldValue(name, value, obj.id);
        }
        public void SetFieldValue<T>(FieldID field, T value)
        {
            SerializedField fieldData = objectFields[field.id];

            Internal_DeleteThing(fieldData.type, fieldData.index);
            SerializedTypes sType = Internal_FindSerializedType(value.GetType());

            int index = Internal_RegisterThing(sType, value);
            objectFields[field.id] = new SerializedField(sType, index, fieldData.name, fieldData.next);
        }
        public T GetFieldValue<T>(ObjectID obj, string name)
        {
            return (T)Internal_GetObjectFieldValue(typeof(T), name, obj.id);
        }
        public object GetFieldValue(ObjectID obj, string name, Type t)
        {
            return Internal_GetObjectFieldValue(t, name, obj.id);
        }
        public T GetFieldValue<T>(FieldID field)
        {
            SerializedField fieldData = objectFields[field.id];
            if (Internal_IsCompatible<T>(fieldData.type))
            {
                return (T)Internal_ConvertThingTo(typeof(T), fieldData.type, fieldData.index);
            }

            throw new Exception($"Can't convert field of serialized type {fieldData.type} to type {typeof(T)}");
        }

        public T GetFieldValueOrDefault<T>(ObjectID obj, string name, T defaultValue)
        {
            if (!Internal_TryFindField(name, obj.id, out int fieldIndex))
            {
                return defaultValue;
            }

            SerializedField field = objectFields[fieldIndex];

            if (!Internal_IsCompatible(typeof(T), field.type))
            {
                return defaultValue;
            }

            return (T)Internal_ConvertThingTo(typeof(T), field.type, field.index);
        }
        public T GetCollectionElement<T>(CollectionID collection, int index)
        {
            SerializedCollection collectionData = collections[collection.id];
            if (collectionData.length <= index) throw new IndexOutOfRangeException();

            int next = collectionData.next;
            for (int i = 0; i < index -1; i++)
            {
                next = collectionElements[next].next;
            }

            SerializedCollectionElement element = collectionElements[next];

            if (Internal_IsCompatible<T>(element.type))
            {
                return (T)Internal_ConvertThingTo(typeof(T), element.type, element.index);
            }

            throw new Exception($"Can't convert field of serialized type {element.type} to type {typeof(T)}");
        }
        public void AddCollectionElement<T>(CollectionID collection, T value)
        {
            SerializedCollection collectionData = collections[collection.id];
            int last = -1;
            int next = collectionData.next;

            while (next != -1)
            {
                last = next;
                next = collectionElements[next].next;
            }

            SerializedTypes sType = Internal_FindSerializedType(typeof(T));
            int newElement = collectionElements.Add(new SerializedCollectionElement(sType, Internal_RegisterThing(sType, value), -1));

            if (last == -1)
            {
                collections[collection.id] = new SerializedCollection(newElement, collectionData.length +1);
            }
            else
            {
                SerializedCollectionElement lastElement = collectionElements[last];
                collectionElements[last] = new SerializedCollectionElement(lastElement.type, lastElement.index, newElement);
            }
        }
        public void SetCollectionElement<T>(CollectionID collection, T value, int index)
        {
            SerializedCollection collectionData = collections[collection.id];
            if (collectionData.length <= index) throw new IndexOutOfRangeException();

            int next = collectionData.next;
            for (int i = 0; i < index - 1; i++)
            {
                next = collectionElements[next].next;
            }

            SerializedCollectionElement element = collectionElements[next];
            Internal_DeleteThing(element.type, element.index);
            SerializedTypes sType = Internal_FindSerializedType(typeof(T));
            collectionElements[next] = new SerializedCollectionElement(sType, Internal_RegisterThing(sType, value), element.next);
        }
        public void RemoveCollectionElement<T>(CollectionID collection, int index)
        {
            SerializedCollection collectionData = collections[collection.id];
            if (collectionData.length <= index) throw new IndexOutOfRangeException();

            int parent = -1;
            int removed = collectionData.next;

            for (int i = 0; i < index - 1; i++)
            {
                parent = removed;
                removed = collectionElements[removed].next;
            }

            SerializedCollectionElement removedElement = collectionElements[removed];
            if (parent == -1)
            {
                collections[collection.id] = new SerializedCollection(removedElement.next, collectionData.length - 1);
            }
            else
            {
                SerializedCollectionElement parentElement = collectionElements[parent];
                collectionElements[parent] = new SerializedCollectionElement(parentElement.type, parentElement.index, removedElement.next);

                collections[collection.id] = new SerializedCollection(collectionData.next, collectionData.length - 1);
            }

            Internal_DeleteThing(removedElement.type, removedElement.index);
            collectionElements.Delete(removed);
        }
        public int GetLength(CollectionID collection)
        {
            return collections[collection.id].length;
        }
        public TValue GetDictionaryValue<TKey, TValue>(DictionaryID dictionary, TKey key)
        {
            int index = Internal_TryFindDictionaryEntry(dictionary.id, key.GetHashCode());

            if (index == -1)
            {
                throw new KeyNotFoundException();
            }

            SerializedDictionaryEntry entry = dictionaryEntries[index];
            return (TValue)Internal_ConvertThingTo(typeof(TValue), entry.type, entry.index);
        }
        public bool TryGetDictionaryValue<TKey, TValue>(DictionaryID dictionary, TKey key, out TValue value)
        {
            int index = Internal_TryFindDictionaryEntry(dictionary.id, key.GetHashCode());

            if (index == -1)
            {
                value = default;
                return false;
            }

            SerializedDictionaryEntry entry = dictionaryEntries[index];
            value = (TValue)Internal_ConvertThingTo(typeof(TValue), entry.type, entry.index);
            return true;
        }
        public void SetDictionaryValue<TKey, TValue>(DictionaryID dictionary, TKey key, TValue value)
        {
            int index = Internal_TryFindDictionaryEntry(dictionary.id, key.GetHashCode());

            if (index == -1)
            {
                SerializedTypes valueType = Internal_FindSerializedType(typeof(TValue));
                SerializedTypes keyType = Internal_FindSerializedType(typeof(TKey));
                SerializedDictionary dictionaryData = dictionaries[dictionary.id];

                int newEntryIndex = dictionaryEntries.Add(new SerializedDictionaryEntry(valueType, Internal_RegisterThing(valueType, value), keyType, Internal_RegisterThing(keyType, key), dictionaryData.next, key.GetHashCode()));
                dictionaries[dictionary.id] = new SerializedDictionary(dictionaryData.length +1, newEntryIndex);
            }
            else
            {
                SerializedDictionaryEntry entry = dictionaryEntries[index];
                Internal_DeleteThing(entry.type, entry.index);
                SerializedTypes sType = Internal_FindSerializedType(typeof(TValue));
                entry = new SerializedDictionaryEntry(sType, Internal_RegisterThing(sType, value), entry.keyType, entry.keyIndex, entry.next, entry.keyHash);
            }
        }
        public void RemoveDictionaryValue<TKey, TValue>(DictionaryID dictionary, TKey key)
        {
            int parent = Internal_TryFindPreviousDictionaryEntry(dictionary.id, key.GetHashCode());

            if (parent == -1) return;

            SerializedDictionary dictionaryData = dictionaries[dictionary.id];

            int removed = 0;
            SerializedDictionaryEntry removedEntry;

            if (parent == -2)
            {
                removed = dictionaryData.next;
                removedEntry = dictionaryEntries[removed];

                dictionaries[dictionary.id] = new SerializedDictionary(dictionaryData.length -1, removedEntry.next);
            }
            else
            {
                SerializedDictionaryEntry parentEntry = dictionaryEntries[parent];
                removed = parentEntry.next;
                removedEntry = dictionaryEntries[removed];

                dictionaryEntries[parent] = new SerializedDictionaryEntry(parentEntry.type, parentEntry.index, parentEntry.keyType, parentEntry.keyIndex, removedEntry.next, parentEntry.keyHash);
                dictionaries[dictionary.id] = new SerializedDictionary(dictionaryData.length - 1, dictionaryData.next);
            }

            Internal_DeleteThing(removedEntry.type, removedEntry.index);
            Internal_DeleteThing(removedEntry.keyType, removedEntry.keyIndex);
            dictionaryEntries.Delete(removed);
        }
        public bool ContainsKey<TKey>(DictionaryID dictionary, TKey key)
        {
            return Internal_TryFindDictionaryEntry(dictionary.id, key.GetHashCode()) != -1;
        }
        public int GetCount(DictionaryID dictionary)
        {
            return dictionaries[dictionary.id].length;
        }
        int collectionEnumerator = 0;
        public void StartEnumeration(CollectionID collection)
        {
            collectionEnumerator = collections[collection.id].next;
        }
        public bool Next<T>(out T element)
        {
            element = default;
            if (collectionEnumerator == -1) return false;

            SerializedCollectionElement collectionElement = collectionElements[collectionEnumerator];
            element = (T)Internal_ConvertThingTo(typeof(T), collectionElement.type, collectionElement.index);
            collectionEnumerator = collectionElement.next;
            return true;
        }
        int dictionaryEnumerator = 0;
        public void StartEnumeration(DictionaryID dictionary)
        {
            dictionaryEnumerator = dictionaries[dictionary.id].next;
        }
        public bool Next<TKey, TValue>(out TKey key, out TValue value)
        {
            key = default;
            value = default;
            if (dictionaryEnumerator == -1) return false;

            SerializedDictionaryEntry dictionaryEntry = dictionaryEntries[dictionaryEnumerator];
            key = (TKey)Internal_ConvertThingTo(typeof(TKey), dictionaryEntry.keyType, dictionaryEntry.keyIndex);
            value = (TValue)Internal_ConvertThingTo(typeof(TValue), dictionaryEntry.type, dictionaryEntry.index);
            dictionaryEnumerator = dictionaryEntry.next;
            return true;
        }
        #endregion

        #region ISerializer
        public void AddField<T>(ObjectID obj, string name, T value)
        {
            SerializedTypes sType = Internal_FindSerializedType(value.GetType());
            Internal_AttachFieldToObject(name, sType, Internal_RegisterThing(sType, value), obj.id);
        }
        public void Serialize(ObjectID id, object value)
        {
            Internal_DefaultSerialize(id.id, value);
        }
        public object Deserialize(ObjectID id, Type type)
        {
            return Internal_DefaultDeserialize(type, id.id);
        }

        #endregion

        #region Read/Write
        internal int Internal_ReadBytes(ByteReader reader, SerializedTypes type)
        {
            switch (type)
            {
                case SerializedTypes.@object:
                    return Internal_ReadObjectBytes(reader);
                case SerializedTypes.@collection:
                    return Internal_ReadCollectionBytes(reader);
                case SerializedTypes.@string:
                    return Internal_RegisterString(reader.ReadString());
                case SerializedTypes.@dictionary:
                    return Internal_ReadDictionaryBytes(reader);
                case SerializedTypes.@number:
                    return numbers.Add(reader.ReadDouble());
                case SerializedTypes.@bool:
                    return bools.Add(reader.ReadBool());
                case SerializedTypes.@char:
                    return chars.Add(reader.ReadChar());
            }

            return -1;
        }

        internal int Internal_ReadObjectBytes(ByteReader reader)
        {
            int length = reader.ReadInt32();
            int objIndex = objects.Add(new SerializedObject(-1, length, version));

            for (int i = 0; i < length; i++)
            {
                string name = reader.ReadString();
                SerializedTypes fieldType = (SerializedTypes)reader.ReadByte();
                Internal_AttachFieldToObject(name, fieldType, Internal_ReadBytes(reader, fieldType), objIndex);
            }

            return objIndex;
        }

        internal int Internal_ReadCollectionBytes(ByteReader reader)
        {
            int length = reader.ReadInt32();
            int lastIndex = 0;
            SerializedCollectionElement last = new();
            int collectionIndex = collections.Add(new SerializedCollection(-1, length));

            for (int i = 0; i < length; i++)
            {
                SerializedTypes type = (SerializedTypes)reader.ReadByte();
                int index = Internal_ReadBytes(reader, type);
                SerializedCollectionElement next = new SerializedCollectionElement(type, index, -1);
                int nextIndex = collectionElements.Add(next);

                if (i == 0)
                {
                    collections[collectionIndex] = new SerializedCollection(nextIndex, length);
                }
                else
                {
                    collectionElements[lastIndex] = new SerializedCollectionElement(last.type, last.index, nextIndex);
                }

                lastIndex = nextIndex;
                last = next;
            }

            return collectionIndex;
        }
        internal int Internal_ReadDictionaryBytes(ByteReader reader)
        {
            int length = reader.ReadInt32();
            int next = -1;

            for (int i = 0; i < length; i++)
            {
                int keyHash = reader.ReadInt32();
                SerializedTypes keyType = (SerializedTypes)reader.ReadByte();
                int keyIndex = Internal_ReadBytes(reader, keyType);
                SerializedTypes valueType = (SerializedTypes)reader.ReadByte();
                int valueIndex = Internal_ReadBytes(reader, valueType);
                next = dictionaryEntries.Add(new SerializedDictionaryEntry(valueType, valueIndex, keyType, keyIndex, next, keyHash));
            }
            int dictionaryIndex = dictionaries.Add(new SerializedDictionary(length, next));

            return dictionaryIndex;
        }

        internal void Internal_WriteBytes(ByteWriter writer, SerializedTypes type, int index)
        {
            writer.Write((byte)type);
            switch (type)
            {
                case SerializedTypes.@object:
                    Internal_WriteObjectBytes(writer, index);
                    break;
                case SerializedTypes.@collection:
                    Internal_WriteCollectionBytes(writer, index);
                    break;
                case SerializedTypes.@string:
                    writer.Write(Internal_GetString(index));
                    break;
                case SerializedTypes.@dictionary:
                    Internal_WriteDictionaryBytes(writer, index);
                    break;
                case SerializedTypes.@number:
                    writer.Write(numbers[index]);
                    break;
                case SerializedTypes.@bool:
                    writer.Write(bools[index]);
                    break;
                case SerializedTypes.@char:
                    writer.Write(chars[index]);
                    break;
            }
        }

        internal void Internal_WriteObjectBytes(ByteWriter writer, int index)
        {
            SerializedObject obj = objects[index];
            int length = obj.length;
            writer.Write(length);

            int nextField = obj.next;
            while (nextField != -1)
            {
                SerializedField field = objectFields[nextField];
                writer.Write(Internal_GetString(field.name));
                Internal_WriteBytes(writer, field.type, field.index);
                nextField = field.next;
            }
        }

        internal void Internal_WriteDictionaryBytes(ByteWriter writer, int index)
        {
            SerializedDictionary dictionary = dictionaries[index];
            int length = dictionary.length;
            writer.Write(length);

            int nextEntry = dictionary.next;
            while (nextEntry != -1)
            {
                SerializedDictionaryEntry entry = dictionaryEntries[nextEntry];
                writer.Write(entry.keyHash);
                Internal_WriteBytes(writer, entry.keyType, entry.keyIndex);
                Internal_WriteBytes(writer, entry.type, entry.index);
                nextEntry = entry.next;
            }
        }

        internal void Internal_WriteCollectionBytes(ByteWriter writer, int index)
        {
            SerializedCollection collection = collections[index];
            writer.Write(collection.length);

            int nextElement = collection.next;
            while (nextElement != -1)
            {
                SerializedCollectionElement element = collectionElements[nextElement];
                Internal_WriteBytes(writer, element.type, element.index);
                nextElement = element.next;
            }
        }
        #endregion

        #region public static members
        public static T FromBytes<T>(Span<byte> bytes)
        {
            ByteReader reader = new(bytes);
            int version = reader.ReadInt32();
            Versionizer converter = staticConverter;
            staticConverter.Clear();
            converter.Internal_ReadBytes(reader, SerializedTypes.@object);
            converter.version = version;
            converter.Internal_Update(typeof(T), 0);

            return (T)converter.Internal_DeserializeObject(typeof(T), 0);
        }

        public static Span<byte> ToBytes(object obj)
        {
            Versionizer converter = staticConverter;
            staticConverter.Clear();
            converter.Internal_SerializeObject(obj);
            ByteWriter writer = new();
            writer.Write(converter.latestVersion);
            converter.Internal_WriteObjectBytes(writer, 0);
            return writer.ToSpan();
        }

        public static void SetSerializer(Type type, Type serializerType)
        {
            if (!serializerType.IsAssignableTo(typeof(VersionedSerializer)))
            {
                throw new Exception($"Type {serializerType} doesn't inherit from VersionedSerializer");
            }

            VersionedSerializer serializer = (serializerType == typeof(VersionedSerializer) ? Activator.CreateInstance(serializerType, [type]) : Activator.CreateInstance(serializerType)) as VersionedSerializer;
            serializers[type] = serializer;
            
            serializer.InitVersionizer();
        }
        #endregion



        #region Objects

        internal object Internal_GetObjectFieldValue(Type t, string fieldName, int objectID)
        {
            int fieldIndex = Internal_FindField(fieldName, objectID);
            SerializedField obj = objectFields[fieldIndex];

            if (t == typeof(ObjectID) && obj.type == SerializedTypes.@object) return new ObjectID(obj.index);

            return Internal_ConvertThingTo(t, obj.type, obj.index);
        }
        internal void Internal_SetObjectFieldValue(string fieldName, object value, int objectID)
        {
            int fieldIndex = Internal_FindField(fieldName, objectID);
            SerializedField field = objectFields[fieldIndex];

            Internal_DeleteThing(field.type, field.index);
            SerializedTypes sType = Internal_FindSerializedType(value.GetType());

            int index = Internal_RegisterThing(sType, value);
            objectFields[fieldIndex] = new SerializedField(sType, index, field.name, field.next);
        }

        internal void Internal_RenameObjectField(string name, string newName, int objectID)
        {
            int fieldIndex = Internal_FindField(name, objectID);
        
            SerializedField field = objectFields[fieldIndex];
            Internal_DeleteString(field.name);
            objectFields[fieldIndex] = new SerializedField(field.type, field.index, Internal_RegisterString(newName), field.next);
        }

        internal void Internal_RemoveObjectField(string name, int objectID)
        {
            SerializedObject obj = objects[objectID];
            if (obj.next == -1) return;

            int parentFieldIndex = Internal_FindFieldPrevious(name, objectID);
        
            if (parentFieldIndex == -1)
            {
                SerializedField field = objectFields[obj.next];
                objectFields.Delete(obj.next);
                Internal_DeleteThing(field.type, field.index);
                Internal_DeleteString(field.name);
                objects[objectID] = new SerializedObject(field.next, obj.length -1, obj.version);
            }
            else
            {
                SerializedField parentField = objectFields[parentFieldIndex];
                SerializedField field = objectFields[parentField.next];
                int parentFieldNewNext = field.next;
                objectFields.Delete(parentField.next);
                Internal_DeleteThing(field.type, field.index);
                Internal_DeleteString(field.name);
                objectFields[parentFieldIndex] = new SerializedField(parentField.type, parentField.index, parentField.name, parentFieldNewNext);
            }
        }

        internal int Internal_AttachFieldToObject(string name, SerializedTypes type, int index, int objectID)
        {
            int fieldIndex = objectFields.Add(new SerializedField(type, index, Internal_RegisterString(name), -1));
            int parentIndex = Internal_FindLastFieldIndex(objectID);
            SerializedObject obj = objects[objectID];

            if (parentIndex == -1) // no field, attach to head
            {
                objects[objectID] = new SerializedObject(fieldIndex, obj.length +1, obj.version);
            }
            else
            {
                SerializedField parent = objectFields[parentIndex];
                objectFields[parentIndex] = new SerializedField(parent.type, parent.index, parent.name, fieldIndex);
                objects[objectID] = new SerializedObject(obj.next, obj.length +1, obj.version);
            }

            return fieldIndex;
        }

        internal void Internal_DeleteObject(int index)
        {
            SerializedObject obj = objects[index];
            int fieldIndex = obj.next;

            while (fieldIndex != -1)
            {
                SerializedField field = objectFields[fieldIndex];
                objectFields.Delete(fieldIndex);
                Internal_DeleteThing(field.type, field.index);
                Internal_DeleteString(field.name);
                fieldIndex = field.next;
            }

            objects.Delete(index);
        }

        internal object Internal_DeserializeObject(Type type, int index)
        {
            if (type == typeof(ObjectID)) return new ObjectID(index);
            else if (context == Context.transforming)
            {
                throw new Exception("Cannot deserialize class during transformation, fetch the provided ObjectID struct instead");
            }

            VersionedSerializer serializer = Internal_GetSerializer(type);
            Internal_Update(type, index);

            return serializer.Deserialize(this, type, new ObjectID(index));
        }

        internal object Internal_DefaultDeserialize(Type type, int index)
        {
            object deserialized = Activator.CreateInstance(type);
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

            index = objects[index].next;

            while (index != -1)
            {
                SerializedField obj = this.objectFields[index];
                FieldInfo? fieldInfo = type.GetField(Internal_GetString(obj.name));

                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(deserialized, Internal_ConvertThingTo(fieldInfo.FieldType, obj.type, obj.index));
                }

                index = obj.next;
            }

            return deserialized;
        }

        internal void Internal_DefaultSerialize(int objectID, object obj)
        {
            Type type = obj.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                SerializedTypes sType = Internal_FindSerializedType(field.FieldType);
                Internal_AttachFieldToObject(field.Name, sType, Internal_RegisterThing(sType, field.GetValue(obj)), objectID);
            }
        }

        internal int Internal_SerializeObject(object obj)
        {
            Type type = obj.GetType();

            if (obj is ObjectID o)
            {
                return o.id;
            }
            else if (context == Context.transforming)
            {
                throw new Exception("Cannot serialize class during transformation, use the provided CreateObject<T> method instead");
            }

            VersionedSerializer serializer = Internal_GetSerializer(type);
            int index = objects.Add(new SerializedObject(-1, 0, latestVersion));

            serializer.Serialize(obj, this, new ObjectID(index));

            return index;
        }

        internal int Internal_FindLastFieldIndex(int objectID)
        {
            int result = objectID;
            SerializedObject obj = objects[objectID];

            int fieldIndex = obj.next;
            while (fieldIndex != -1)
            {
                SerializedField field = objectFields[fieldIndex];
                if (field.next == -1)
                {
                    return fieldIndex;
                }
                fieldIndex = field.next;
            }
            return -1;
        }

        //internal int Internal_FindFieldPrevious(FieldID fieldID, ObjectID id)
        //{
        //    SerializedObject obj = objects[id.id];
        //    int result = id.id;
        //    int fieldIndex = obj.next;
        //    int prev = -1;
        //
        //    while (fieldIndex != -1)
        //    {
        //        prev = fieldIndex;
        //        SerializedField field = objectFields[fieldIndex];
        //        if (strings[field.name].next == fieldIndex)
        //        {
        //            return prev;
        //        }
        //        fieldIndex = field.next;
        //    }
        //
        //    throw new Exception($"Couldn't find object member with id {fieldID}");
        //}

        internal int Internal_FindFieldPrevious(string name, int objectID)
        {
            SerializedObject obj = objects[objectID];
            int result = objectID;
            int hash = name.GetHashCode();
            int fieldIndex = obj.next;
            int prev = -1;

            while (fieldIndex != -1)
            {
                SerializedField field = objectFields[fieldIndex];
                if (strings[field.name].hash == hash)
                {
                    return prev;
                }
                prev = fieldIndex;
                fieldIndex = field.next;
            }

            throw new Exception($"Couldn't find object member with name {name}");
        }

        internal bool Internal_TryFindField(string name, int objectID, out int fieldIndex)
        {
            SerializedObject obj = objects[objectID];
            int result = objectID;
            int hash = name.GetHashCode();
            fieldIndex = obj.next;

            while (fieldIndex != -1)
            {
                SerializedField field = objectFields[fieldIndex];
                if (strings[field.name].hash == hash)
                {
                    return true;
                }
                fieldIndex = field.next;
            }

            return false;
        }

        internal int Internal_FindField(string name, int objectID)
        {
            SerializedObject obj = objects[objectID];
            int result = objectID;
            int hash = name.GetHashCode();
            int fieldIndex = obj.next;

            while (fieldIndex != -1)
            {
                SerializedField field = objectFields[fieldIndex];
                if (strings[field.name].hash == hash)
                {
                    return fieldIndex;
                }
                fieldIndex = field.next;
            }


            throw new Exception($"Couldn't find object member with name {name}");
        }


        #endregion

        #region Strings
        internal int Internal_RegisterString(string str)
        {
            int hash = str.GetHashCode();
            int length = str.Length;
            int index = -1;

            for (int i = str.Length-1; i >= 0; i--)
            {
                index = stringChars.Add(new SerializedStringChar(str[i], index));
            }

            int head = strings.Add(new SerializedString(index, length, hash));
            return head;
        }

        internal void Internal_DeleteString(int index)
        {
            SerializedString str = strings[index];
            strings.Delete(index);
            index = str.next;

            while (index != -1)
            {
                SerializedStringChar c = stringChars[index];
                stringChars.Delete(index);
                index = c.next;
            }
        }

        internal string Internal_GetString(int index)
        {
            SerializedString head = strings[index];
            int l = head.length;
            char[] chars = new char[l];
            int charIndex = head.next;

            for (int i = 0; i < l; i++)
            {
                SerializedStringChar c = stringChars[charIndex];
                chars[i] = c.c;
                charIndex = c.next;
            }

            return new string(chars);
        }
        #endregion

        #region Numbers
        internal double Internal_NumberToDouble(object number)
        {
            if (number is byte b) return b;
            if (number is sbyte sb) return sb;
            if (number is short s) return s;
            if (number is int i) return i;
            if (number is long l) return l;
            if (number is float f) return f;
            if (number is double d) return d;
            if (number is decimal dec) return (double)dec;
            if (number is ushort us) return us;
            if (number is uint ui) return ui;
            if (number is ulong ul) return ul;

            throw new Exception($"Couldn't convert number of type {number.GetType()} to decimal");
        }

        internal object Internal_DoubleToNumber(Type numType, double d)
        {
            if (numType == typeof(byte)) return (byte)d;
            if (numType == typeof(sbyte)) return (sbyte)d;
            if (numType == typeof(short)) return (short)d;
            if (numType == typeof(int)) return (int)d;
            if (numType == typeof(long)) return (long)d;
            if (numType == typeof(float)) return (float)d;
            if (numType == typeof(double)) return (double)d;
            if (numType == typeof(decimal)) return (decimal)d;
            if (numType == typeof(ushort)) return (ushort)d;
            if (numType == typeof(uint)) return (uint)d;
            if (numType == typeof(ulong)) return (ulong)d;

            throw new Exception($"Couldn't convert decimal to {numType}");
        }

        #endregion

        #region Collections

        internal object Internal_ConvertCollectionTo(Type t, int index)
        {
            if (t == typeof(CollectionID)) return new CollectionID(index);

            if (t.IsArray)
            {
                Type elementType = t.GetElementType();
                SerializedCollection collection = collections[index];
                int l = collection.length;
                index = collection.next;

                dynamic array = Array.CreateInstance(elementType, l);

                for (int i = 0; i < l; i++)
                {
                    SerializedCollectionElement element = collectionElements[index];
                    dynamic v = Internal_ConvertThingTo(elementType, element.type, element.index);
                    array[i] = v;
                    index = element.next;
                }
                return array;
            }
            else if (t.IsGenericType)
            {
                Type genericTypeDef = t.GetGenericTypeDefinition();
                Type elementType = t.GenericTypeArguments[0];
                SerializedCollection collection = collections[index];
                int l = collection.length;
                index = collection.next;

                if (t.IsAssignableTo(typeof(IList)))
                {
                    object list = Activator.CreateInstance(t);

                    if (list is IList iList)
                    {
                        for (int i = 0; i < l; i++)
                        {
                            SerializedCollectionElement element = collectionElements[index];
                            dynamic v = Internal_ConvertThingTo(elementType, element.type, element.index);
                            iList.Add(v);
                            index = element.next;
                        }
                    }
                    
                    return list;
                }
            }

            throw new Exception($"Couldn't convert object of type {t} to collection");

        }

        internal int Internal_SerializeCollection(dynamic value)
        {
            Type t = value.GetType();

            if (value is CollectionID c)
            {
                return c.id;
            }

            if (t.IsArray)
            {
                Type elementType = t.GetElementType();
                SerializedTypes elementSerializedType = Internal_FindSerializedType(elementType);
                dynamic array = value;
                int index = -1;
                int length = array.Length;

                for (int i = length -1; i >= 0; i--)
                {
                    index = collectionElements.Add(new SerializedCollectionElement(elementSerializedType, Internal_RegisterThing(elementSerializedType, array[i]), index));
                }

                return collections.Add(new SerializedCollection(index, length));
            }
            else if (t.IsGenericType)
            {
                Type genericTypeDef = t.GetGenericTypeDefinition();
                Type elementType = t.GenericTypeArguments[0];
                SerializedTypes elementSerializedType = Internal_FindSerializedType(elementType);

                if (value is IList list)
                {
                    int index = -1;
                    int length = list.Count;

                    for (int i = length - 1; i >= 0; i--)
                    {
                        index = collectionElements.Add(new SerializedCollectionElement(elementSerializedType, Internal_RegisterThing(elementSerializedType, list[i]), index));
                    }

                    return collections.Add(new SerializedCollection(index, length));
                }
            }

            throw new Exception($"Couldn't convert object of type {t} to collection");
        }

        internal void Internal_DeleteCollection(int index)
        {
            SerializedCollection collection = collections[index];
            int elementIndex = collection.next;

            while (elementIndex != -1)
            {
                SerializedCollectionElement element = collectionElements[elementIndex];
                collectionElements.Delete(elementIndex);
                Internal_DeleteThing(element.type, element.index);
                elementIndex = element.next;
            }

            collections.Delete(index);
        }

        internal static bool Internal_IsTypeCollection(Type t)
        {
            if (t.IsArray) return true;
            if (t.IsGenericType)
            {
                Type genericType = t.GetGenericTypeDefinition();
                if (genericType == typeof(List<>)) return true;
            }

            return false;
        }


        #endregion

        #region Dictionaries

        internal void Internal_DeleteDictionary(int index)
        {
            SerializedDictionary dictionary = dictionaries[index];
            int entryIndex = dictionary.next;

            while (entryIndex != -1)
            {
                SerializedDictionaryEntry entry = dictionaryEntries[entryIndex];
                int next = entry.next;
                Internal_DeleteThing(entry.type, entry.index);
                Internal_DeleteThing(entry.keyType, entry.keyIndex);
                dictionaryEntries.Delete(entryIndex);
                entryIndex = next;
            }

            dictionaries.Delete(index);
        }

        internal int Internal_SerializeDictionary(dynamic value)
        {
            Type t = value.GetType();

            if (value is DictionaryID d)
            {
                return d.id;
            }

            if (t.IsGenericType && t.GenericTypeArguments.Length == 2)
            {
                Type keyType = t.GenericTypeArguments[0];
                Type elementType = t.GenericTypeArguments[1];

                SerializedTypes keySType = Internal_FindSerializedType(keyType);
                SerializedTypes elementSType = Internal_FindSerializedType(elementType);
                int index = -1;

                foreach (dynamic item in value)
                {
                    index = dictionaryEntries.Add(new SerializedDictionaryEntry(elementSType, Internal_RegisterThing(elementSType, item.Value), keySType, Internal_RegisterThing(keySType, item.Key), index, item.Key.GetHashCode()));
                }

                return dictionaries.Add(new SerializedDictionary(value.Count, index));
            }

            throw new Exception($"Couldn't serialize object of type {t} to dictionary");
        }

        internal int Internal_TryFindDictionaryEntry(int id, int hash)
        {
            int index = dictionaries[id].next;
            while (index != -1) {
                SerializedDictionaryEntry entry = dictionaryEntries[index];
                if (entry.keyHash == hash) return index;
                index = entry.next;
            }
            return -1;
        }

        internal int Internal_TryFindPreviousDictionaryEntry(int id, int hash)
        {
            int index = dictionaries[id].next;
            int previous = -2;
            while (index != -1)
            {
                SerializedDictionaryEntry entry = dictionaryEntries[index];
                if (entry.keyHash == hash) return previous;
                previous = index;
                index = entry.next;
            }
            return -1;
        }

        internal object Internal_DeserializeDictionaryTo(Type t, int index)
        {
            if (t == typeof(DictionaryID)) return new DictionaryID(index);

            SerializedDictionary serialized = dictionaries[index];

            if (t.IsGenericType && t.GenericTypeArguments.Length == 2)
            {
                Type keyType = t.GenericTypeArguments[0];
                Type elementType = t.GenericTypeArguments[1];
                int l = serialized.length;
                index = serialized.next;

                dynamic dict = Activator.CreateInstance(t);

                for (int i = 0; i < l; i++)
                {
                    SerializedDictionaryEntry entry = dictionaryEntries[index];
                    dynamic key = Internal_ConvertThingTo(keyType, entry.keyType, entry.keyIndex);
                    dynamic value = Internal_ConvertThingTo(elementType, entry.type, entry.index);

                    dict.Add(key, value);
                    index = entry.next;
                }

                return dict;
            }

            throw new Exception($"Couldn't deserialize dictionary to type {t}");
        }
        #endregion

        #region utility
        internal void Internal_Update(Type type, int objectId)
        {
            SerializedObject obj = objects[objectId];
            VersionedSerializer serializer = Internal_GetSerializer(type);


            for (int i = obj.version + 1; i <= latestVersion; i++)
            {
                int fieldIndex = obj.next;
                while (fieldIndex != -1)
                {
                    SerializedField field = objectFields[fieldIndex];
                    fieldIndex = field.next;

                    if (field.type == SerializedTypes.@object)
                    {
                        Type fieldType = serializer.GetFieldTypeAtVersion(strings[field.name].hash, i);
                        Internal_UpdateTo(fieldType, field.index, i);
                    }
                }
                if (serializer.versionMapping.TryGetValue(i, out var v))
                {
                    Version ver = serializer.versions[v];
                    ver.Update(this, new ObjectID(objectId));
                    objects[objectId] = new SerializedObject(obj.next, obj.length, i);
                }
            }
        }
        internal void Internal_UpdateTo(Type type, int objectId, int targetVersion)
        {
            SerializedObject obj = objects[objectId];
            VersionedSerializer serializer = Internal_GetSerializer(type);

            for (int i = obj.version + 1; i <= targetVersion; i++)
            {
                int fieldIndex = obj.next;
                while (fieldIndex != -1)
                {
                    SerializedField field = objectFields[fieldIndex];
                    fieldIndex = field.next;

                    if (field.type == SerializedTypes.@object)
                    {
                        Type fieldType = serializer.GetFieldTypeAtVersion(strings[field.name].hash, i);
                        Internal_UpdateTo(fieldType, field.index, i);
                    }
                }
                if (serializer.versionMapping.TryGetValue(i, out var v))
                {
                    Version ver = serializer.versions[v];
                    ver.Update(this, new ObjectID(objectId));
                    objects[objectId] = new SerializedObject(obj.next, obj.length, i);
                }
            }
        }
        internal int Internal_RegisterDefaultObjectAtVersion(Type type, int version)
        {
            VersionedSerializer serializer = Internal_GetSerializer(type);
            Version v = serializer.GetLatestCompatibleVersion(version);

            int nextFieldIndex = -1;

            foreach (var field in v.signature)
            {
                SerializedTypes sType = Internal_FindSerializedType(field.Value.type);
                nextFieldIndex = objectFields.Add(new SerializedField(sType, Internal_RegisterDefaultThingAtVersion(field.Value.type, version), Internal_RegisterString(field.Value.name), nextFieldIndex));
            }

            return objects.Add(new SerializedObject(nextFieldIndex, v.signature.Count, version));
        }
        internal VersionedSerializer Internal_GetSerializer(Type type)
        {
            if (!serializers.TryGetValue(type, out var serializer))
            {
                var attribute = type.GetCustomAttribute<VersionedSerializerAttribute>();
                if (attribute == null)
                {
                    SetSerializer(type, typeof(VersionedSerializer));
                }
                else
                {
                    SetSerializer(type, attribute.serializer);
                }
                serializer = serializers[type];

                // also loading all of the object's fields types recursively
                foreach (var field in serializer.versions[serializer.latestVersion].signature)
                {
                    if (field.Value.type.IsClass | field.Value.type.IsAnsiClass)
                    {
                        Internal_GetSerializer(field.Value.type);
                    }
                }
            }
            latestVersion = int.Max(serializer.latestVersion, latestVersion);
            return serializer;
        }
        internal int Internal_RegisterThing(SerializedTypes sType, object value)
        {
            switch (sType)
            {
                case SerializedTypes.@object:
                    return Internal_SerializeObject(value);
                case SerializedTypes.@collection:
                    return Internal_SerializeCollection(value);
                case SerializedTypes.@string:
                    if (value is string s) return Internal_RegisterString(s);
                    break;
                case SerializedTypes.@dictionary:
                    return Internal_SerializeDictionary(value);
                case SerializedTypes.@number:
                    return numbers.Add(Internal_NumberToDouble(value));
                case SerializedTypes.@bool:
                    if (value is bool b) return bools.Add(b);
                    break;
                case SerializedTypes.@char:
                    if (value is char c) return chars.Add(c);
                    break;
            }

            throw new Exception($"Couldn't register thing of serialized type {sType}");
        }
        internal int Internal_RegisterDefaultThingAtVersion(Type type, int version)
        {
            SerializedTypes sType = Internal_FindSerializedType(type);

            switch (sType)
            {
                case SerializedTypes.@object:
                    return Internal_RegisterDefaultObjectAtVersion(type, version);
                case SerializedTypes.@collection:
                    return collections.Add(new SerializedCollection(-1, 0));
                case SerializedTypes.@string:
                    return strings.Add(new SerializedString(-1, 0, 0));
                case SerializedTypes.@dictionary:
                    return dictionaries.Add(new SerializedDictionary(0, -1));
                case SerializedTypes.@number:
                    return numbers.Add(0);
                case SerializedTypes.@bool:
                    return bools.Add(false);
                case SerializedTypes.@char:
                    return chars.Add('a');
            }

            throw new Exception($"Couldn't register thing of serialized type {sType}");
        }
        internal void Internal_DeleteThing(SerializedTypes type, int index)
        {
            switch (type)
            {
                case SerializedTypes.@object:
                    Internal_DeleteObject(index);
                    break;
                case SerializedTypes.@collection:
                    Internal_DeleteCollection(index);
                    break;
                case SerializedTypes.@string:
                    Internal_DeleteString(index);
                    break;
                case SerializedTypes.@dictionary:
                    Internal_DeleteDictionary(index);
                    break;
                case SerializedTypes.@number:
                    numbers.Delete(index);
                    break;
                case SerializedTypes.@bool:
                    bools.Delete(index);
                    break;
                case SerializedTypes.@char:
                    chars.Delete(index);
                    break;
            }
        }
        internal object Internal_ConvertThingTo(Type t, SerializedTypes type, int index)
        {
            if (!Internal_IsCompatible(t, type))
            {
                throw new Exception($"Cannot convert serialized type {type} to type {t}");
            }

            switch (type)
            {
                case SerializedTypes.@object:
                    return Internal_DeserializeObject(t, index);
                case SerializedTypes.@collection:
                    return Internal_ConvertCollectionTo(t, index);
                case SerializedTypes.@string:
                    return Internal_GetString(index);
                case SerializedTypes.@dictionary:
                    return Internal_DeserializeDictionaryTo(t, index);
                case SerializedTypes.@number:
                    return Internal_DoubleToNumber(t, numbers[index]);
                case SerializedTypes.@bool:
                    return bools[index];
                case SerializedTypes.@char:
                    return chars[index];
            }

            throw new Exception($"Could not convert type {t} to any serialized type");
        }
        internal void Internal_NullCheck(object obj)
        {
            if (obj == null)
            {
                throw new Exception("Value cannot be null !");
            }
        }
        internal static SerializedTypes Internal_FindSerializedType(Type t)
        {
            if (t == typeof(bool)) return SerializedTypes.@bool;
            if (t == typeof(char)) return SerializedTypes.@char;
            if (t == typeof(string)) return SerializedTypes.@string;
            if (t.IsPrimitive && t.IsValueType) return SerializedTypes.number;
            if (t == typeof(CollectionID) || Internal_IsTypeCollection(t)) return SerializedTypes.collection;
            if (t == typeof(DictionaryID) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))) return SerializedTypes.dictionary;
            if (t == typeof(ObjectID) || (t.IsClass || t.IsAnsiClass)) return SerializedTypes.@object;

            throw new Exception($"Could not convert type {t} to any serialized type");
        }
        internal bool Internal_IsCompatible(Type t, SerializedTypes type)
        {
            switch (type)
            {
                case SerializedTypes.@object:
                    return t == typeof(ObjectID) || (t.IsClass || t.IsAnsiClass);
                case SerializedTypes.@collection:
                    return t == typeof(CollectionID) || Internal_IsTypeCollection(t);
                case SerializedTypes.@string:
                    return t == typeof(string);
                case SerializedTypes.@dictionary:
                    return t == typeof(DictionaryID) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>));
                case SerializedTypes.@number:
                    return t.IsPrimitive && t.IsValueType && t != typeof(char) && t != typeof(bool);
                case SerializedTypes.@bool:
                    return t == typeof(bool);
                case SerializedTypes.@char:
                    return t == typeof(char);
            }

            return false;
        }
        internal bool Internal_IsCompatible<T>(SerializedTypes type)
        {
            return Internal_IsCompatible(typeof(T), type);
        }
        #endregion
    }
    internal enum SerializedTypes
    {
        @object,
        @collection,
        @string,
        @dictionary,
        @number,
        @bool,
        @char,
    }

    internal enum Context
    {
        serializing,
        transforming,
        versioning
    }

    #region structs
    internal readonly record struct SerializedField(SerializedTypes type, int index, int name, int next);
    internal readonly record struct SerializedObject(int next, int length, int version);
    internal readonly record struct SerializedCollection(int next, int length);
    internal readonly record struct SerializedCollectionElement(SerializedTypes type, int index, int next);
    internal readonly record struct SerializedDictionary(int length, int next);
    internal readonly record struct SerializedDictionaryEntry(SerializedTypes type, int index, SerializedTypes keyType, int keyIndex, int next, int keyHash);
    internal readonly record struct SerializedString(int next, int length, int hash);
    internal readonly record struct SerializedStringChar(char c, int next);
    public readonly record struct ObjectID(int id);
    public readonly record struct FieldID(int id);
    public readonly record struct CollectionID(int id);
    public readonly record struct DictionaryID(int id);
    #endregion
}
