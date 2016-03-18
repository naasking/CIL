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
        /// <param name="code"></param>
        /// <returns></returns>
        public static IEnumerable<Instruction> GetInstructions(this MethodBase code)
        {
            return Read(code.Module, code.GetMethodBody().GetILAsByteArray());
        }

        /// <summary>
        /// Read the instruction stream from the given bytes.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        public static IEnumerable<Instruction> Read(Module module, byte[] code)
        {
            for (int i = 0; i < code.Length; )
            {
                var op = GetOpCode(code, ref i);
                var arg = default(Operand);
                switch (op.OperandType)
                {
                    case OperandType.InlineBrTarget:
                    case OperandType.InlineI:
                    case OperandType.InlineMethod:
                    case OperandType.InlineString:
                    case OperandType.InlineSig:
                    case OperandType.InlineField:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                    case OperandType.InlineSwitch:
                        arg = new Operand(BitConverter.ToInt32(code, i));
                        i += 4;
                        break;
                    case OperandType.InlineI8:
                        arg = new Operand(BitConverter.ToInt64(code, i));
                        i += 8;
                        break;
                    case OperandType.InlineR:
                        arg = new Operand(BitConverter.ToDouble(code, i));
                        i += 8;
                        break;
                    case OperandType.InlineVar:
                        arg = new Operand(BitConverter.ToInt16(code, i));
                        i += 2;
                        break;
                    case OperandType.ShortInlineR:
                        arg = new Operand(BitConverter.ToSingle(code, i));
                        i += 4;
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        arg = new Operand(code[i]);
                        i += 1;
                        break;
                }
                yield return new Instruction(module, op, arg);
            }
        }

        /// <summary>
        /// Normalizes all the special inline instructions into a
        /// single instruction type with an argument.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public static Instruction Simplify(this Instruction op)
        {
            switch (op.OpCode.Type())
            {
                case OpType.Stloc_s: return new Instruction(op.Module, OpCodes.Stloc, op.Operand);
                case OpType.Stloc_0: return new Instruction(op.Module, OpCodes.Stloc, 0);
                case OpType.Stloc_1: return new Instruction(op.Module, OpCodes.Stloc, 1);
                case OpType.Stloc_2: return new Instruction(op.Module, OpCodes.Stloc, 2);
                case OpType.Stloc_3: return new Instruction(op.Module, OpCodes.Stloc, 3);
                case OpType.Ldloc_s: return new Instruction(op.Module, OpCodes.Ldloc, op.Operand);
                case OpType.Ldloc_0: return new Instruction(op.Module, OpCodes.Ldloc, 0);
                case OpType.Ldloc_1: return new Instruction(op.Module, OpCodes.Ldloc, 1);
                case OpType.Ldloc_2: return new Instruction(op.Module, OpCodes.Ldloc, 2);
                case OpType.Ldloc_3: return new Instruction(op.Module, OpCodes.Ldloc, 3);
                case OpType.Ldarg_s: return new Instruction(op.Module, OpCodes.Ldarg, op.Operand);
                case OpType.Ldarg_0: return new Instruction(op.Module, OpCodes.Ldarg, 0);
                case OpType.Ldarg_1: return new Instruction(op.Module, OpCodes.Ldarg, 1);
                case OpType.Ldarg_2: return new Instruction(op.Module, OpCodes.Ldarg, 2);
                case OpType.Ldarg_3: return new Instruction(op.Module, OpCodes.Ldarg, 3);
                case OpType.Ldc_i4_s: return new Instruction(op.Module, OpCodes.Ldc_I4, op.Operand);
                case OpType.Ldc_i4_0: return new Instruction(op.Module, OpCodes.Ldc_I4, 0);
                case OpType.Ldc_i4_1: return new Instruction(op.Module, OpCodes.Ldc_I4, 1);
                case OpType.Ldc_i4_2: return new Instruction(op.Module, OpCodes.Ldc_I4, 2);
                case OpType.Ldc_i4_3: return new Instruction(op.Module, OpCodes.Ldc_I4, 3);
                case OpType.Ldc_i4_4: return new Instruction(op.Module, OpCodes.Ldc_I4, 4);
                case OpType.Ldc_i4_5: return new Instruction(op.Module, OpCodes.Ldc_I4, 5);
                case OpType.Ldc_i4_6: return new Instruction(op.Module, OpCodes.Ldc_I4, 6);
                case OpType.Ldc_i4_7: return new Instruction(op.Module, OpCodes.Ldc_I4, 7);
                case OpType.Ldc_i4_8: return new Instruction(op.Module, OpCodes.Ldc_I4, 8);
                default:
                    return op;
            }
        }
    }
}
