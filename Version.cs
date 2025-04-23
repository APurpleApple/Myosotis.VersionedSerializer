using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    internal class Version
    {
        internal Action<SerializedObject> transformer;
        internal Dictionary<string, Type> signature;
    }
}
