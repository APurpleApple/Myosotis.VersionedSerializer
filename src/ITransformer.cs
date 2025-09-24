using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    public interface ITransformer
    {
        public void SetFieldValue<T>(ObjectID obj, string name, T value);
        public void SetFieldValue<T>(FieldID field, T value);
        public T GetFieldValue<T>(ObjectID obj, string name);
        public object GetFieldValue(ObjectID obj, string name, Type type);
        public T GetFieldValue<T>(FieldID field);
        public T GetFieldValueOrDefault<T>(ObjectID obj, string name, T defaultValue);
        public T GetCollectionElement<T>(CollectionID collection, int index);
        public ObjectID CreateObject<T>();
        public void AddCollectionElement<T>(CollectionID collection, T value);
        public void SetCollectionElement<T>(CollectionID collection, T value, int index);
        public void RemoveCollectionElement<T>(CollectionID collection, int index);
        public int GetLength(CollectionID collection);
        public TValue GetDictionaryValue<TKey, TValue>(DictionaryID dictionary, TKey key);
        public void SetDictionaryValue<TKey, TValue>(DictionaryID dictionary, TKey key, TValue value);
        public void RemoveDictionaryValue<TKey, TValue>(DictionaryID dictionary, TKey key);
        public bool ContainsKey<TKey>(DictionaryID dictionary, TKey key);
        public int GetCount(DictionaryID dictionary);
        public void StartEnumeration(CollectionID collection);
        public bool Next<T>(out T element);
        public void StartEnumeration(DictionaryID dictionary);
        public bool Next<TKey, TValue>(out TKey key, out TValue value);
        public bool TryGetDictionaryValue<TKey, TValue>(DictionaryID dictionary, TKey key, out TValue value);
    }
}
