using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Buffer.ByteBuffers
{
    public enum ByteOrder
    {
        /// <summary>
        /// Default on most Windows systems
        /// </summary>
        LittleEndian = 0,
        BigEndian = 1
    }
}
