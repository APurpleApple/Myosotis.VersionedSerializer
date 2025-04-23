using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    internal enum SerializationType
    {
        unsupported,
        primitive,
        @string,
        @object,
        dictionary,
        collection,
        number,
        @bool,
    }
}
