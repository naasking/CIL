using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

namespace CIL
{
    /// <summary>
    /// Methods for parsing and processing CIL opcodes.
    /// </summary>
    public static partial class IL
    {
        static readonly OpCode[] oneByteOps = new OpCode[256];
        static readonly OpCode[] twoByteOps = new OpCode[256];

        // looks like a nice intro if I ever support more metadata:
        // http://www.codeproject.com/Articles/42649/NET-file-format-Signatures-under-the-hood-Part

        static IL()
        {
            var type = typeof(OpCodes);
            foreach (var x in type.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                var op = (OpCode)x.GetValue(null);
                var target = 0 == (op.Value & 0xFF00) ? oneByteOps : twoByteOps;
                target[op.Value & 0xFF] = op;
            }
        }

        /// <summary>
        /// Get the OpCode of the corresponding byte.
        /// </summary>
        /// <param name="ops"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static OpCode GetOpCode(byte[] ops, ref int index)
        {
            var o = ops[index++];
            return o == 0xFE ? twoByteOps[ops[index++]] : oneByteOps[o];
        }

        /// <summary>
        /// The instruction type of the opcode.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public static OpType Type(this OpCode op)
        {
            return (OpType)op.Value;
        }

        /// <summary>
        /// Get the instructions from the given method.
        /// </summary>
        /// <param name="code">The method to analyze.</param>
        /// <returns>An <seealso cref="ILReader"/> that iterates over the bytecode instructions.</returns>
        public static ILReader GetILReader(this MethodBase code)
        {
            return new ILReader(code);
        }

        /// <summary>
        /// Get the instructions from the given method.
        /// </summary>
        /// <param name="code">The method to analyze.</param>
        /// <returns>A stream of instructions.</returns>
        public static IEnumerable<Instruction> GetInstructions(this MethodBase code)
        {
            var il = new ILReader(code);
            while (il.MoveNext())
                yield return il.Current;
        }
    }
}
