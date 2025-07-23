using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    public interface ISerializer : ITransformer
    {
        public void AddField<T>(ObjectID obj, string name, T value);
        public void Serialize(ObjectID id, object value);
        public object Deserialize(ObjectID id, Type type);
    }
}
