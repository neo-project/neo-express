using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Express.Abstractions
{
    public interface INeoBackend
    {
        void CreateBlockchain(string filename, int count, ushort Port);
    }
}
