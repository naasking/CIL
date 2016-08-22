using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CIL
{
#if DEBUG
    // see "II.23.2 Blobs and signatures" and "II.23.2.3 StandAloneMethodSig" of:
    // http://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf
    public struct CallSignature
    {
        byte[] signature;
        public CallSignature(byte[] signature)
        {
            this.signature = signature;
        }
        public CallingConventions CallingConvention
        {
            // lowest 4 bits of first byte describe the calling convention
            get { return (CallingConventions)(signature[0] & 0x0F); }
        }
    }
#endif
}
