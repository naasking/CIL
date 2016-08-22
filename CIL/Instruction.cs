using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace CIL
{
    /// <summary>
    /// The operand of an instruction.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct Operand
    {
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly double Float64;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(4)] public readonly float Float32;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly long Int64;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly ulong UInt64;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly int Int32;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly uint UInt32;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly short Int16;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly ushort UInt16;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly sbyte Int8;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly byte UInt8;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly char Char;
        /// <summary>
        /// The operand value.
        /// </summary>
        [FieldOffset(0)] public readonly ILReader.Label Label;

        /// <summary>
        /// The operand value.
        /// </summary>
        /// <remarks>Use Module.Resolve* to extract the string, fields, methods, or other values.</remarks>
        [FieldOffset(0)] public readonly int MetadataToken;

        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(ILReader.Label x)
            : this()
        {
            this.Label = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(int x)
            : this()
        {
            this.Int32 = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(uint x)
            : this()
        {
            this.UInt32 = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(short x)
            : this()
        {
            this.Int16 = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(ushort x)
            : this()
        {
            this.UInt16 = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(sbyte x)
            : this()
        {
            this.Int8 = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(byte x)
            : this()
        {
            this.UInt8 = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(float x)
            : this()
        {
            this.Float32 = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(double x)
            : this()
        {
            this.Float64 = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(char x)
            : this()
        {
            this.Char = x;
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public static implicit operator Operand(int x)
        {
            return new Operand(x);
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public static implicit operator Operand(double x)
        {
            return new Operand(x);
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public static implicit operator Operand(float x)
        {
            return new Operand(x);
        }
        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public static implicit operator Operand(long x)
        {
            return new Operand(x);
        }
    }

    /// <summary>
    /// A CIL instruction.
    /// </summary>
    public struct Instruction
    {
        /// <summary>
        /// The instruction opcode.
        /// </summary>
        public readonly OpCode OpCode;
        /// <summary>
        /// The instruction's operand, if any.
        /// </summary>
        public readonly Operand Operand;
        /// <summary>
        /// The ILReader that created this instruction.
        /// </summary>
        readonly ILReader reader;

        /// <summary>
        /// Construct an instruction.
        /// </summary>
        /// <param name="reader">The reader that created this instruction.</param>
        /// <param name="op">The instruction opcode.</param>
        /// <param name="arg">The instruction operand.</param>
        public Instruction(ILReader reader, OpCode op, Operand arg)
        {
            this.reader = reader;
            this.OpCode = op;
            this.Operand = arg;
        }

        /// <summary>
        /// Construct an instruction.
        /// </summary>
        /// <param name="reader">The reader that created this instruction.</param>
        /// <param name="op">The instruction opcode.</param>
        public Instruction(ILReader reader, OpCode op)
            : this(reader, op, default(Operand))
        {
        }
        
        /// <summary>
        /// Resolve the instruction's operand value.
        /// </summary>
        /// <returns>The operand value.</returns>
        public object Resolve()
        {
            switch (OpCode.OperandType)
            {
                case OperandType.InlineNone: return "";
                case OperandType.InlineI8: return Operand.Int64;
                case OperandType.InlineR: return Operand.Float64;
                case OperandType.ShortInlineR: return Operand.Float32;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget: return Operand.Int32;
                case OperandType.InlineSwitch: return Operand.MetadataToken;
                case OperandType.InlineField: return ResolveField();
                case OperandType.InlineMethod: return ResolveMethod();
                case OperandType.InlineSig: return ResolveSignature();
                case OperandType.InlineString: return ResolveString();
                case OperandType.InlineTok: return ResolveMember();
                case OperandType.InlineType: return ResolveType();
                case OperandType.InlineVar: return Operand.Int16;
                case OperandType.ShortInlineBrTarget: return Operand.Int8;
                case OperandType.ShortInlineI: return Operand.Int8;
                case OperandType.ShortInlineVar: return Operand.Int8;
                default: //throw new ArgumentException("Unknown operand type.");
                    return "";
            }
        }

        /// <summary>
        /// Normalizes all the special inline instructions into a
        /// single instruction type with an argument.
        /// </summary>
        /// <returns>A simplified instruction.</returns>
        /// <remarks>
        /// This method preserves the type information, so it does not modify the types
        /// of instruction operands to simplify instructions even further.
        /// </remarks>
        public Instruction Simplify()
        {
            //FIXME: a future simplification might also expand redundant instructions on smaller
            //types to the comparable type with the largest size, ie. _R4=>_R8, _I/_I1=>_I8, etc.
            //case OpType.Ldind_i1: return new Instruction(reader, OpCodes.Ldind_I8, Operand);
            //case OpType.Ldind_u1: return new Instruction(reader, OpCodes.Ldind_U8, Operand);
            //...
            //case OpType.Stelem_i1: return new Instruction(reader, OpCodes.Stelem_I8, Operand);
            //case OpType.Stelem_i2: return new Instruction(reader, OpCodes.Stelem_I8, Operand);
            //case OpType.Stelem_i4: return new Instruction(reader, OpCodes.Stelem_I8, Operand);
            //...
            switch (OpCode.Type())
            {
                case OpType.Starg_s: return new Instruction(reader, OpCodes.Starg, Operand);
                case OpType.Stloc_s: return new Instruction(reader, OpCodes.Stloc, Operand);
                case OpType.Stloc_0: return new Instruction(reader, OpCodes.Stloc, 0);
                case OpType.Stloc_1: return new Instruction(reader, OpCodes.Stloc, 1);
                case OpType.Stloc_2: return new Instruction(reader, OpCodes.Stloc, 2);
                case OpType.Stloc_3: return new Instruction(reader, OpCodes.Stloc, 3);
                case OpType.Ldloca_s: return new Instruction(reader, OpCodes.Ldloca, Operand);
                case OpType.Ldloc_s: return new Instruction(reader, OpCodes.Ldloc, Operand);
                case OpType.Ldloc_0: return new Instruction(reader, OpCodes.Ldloc, 0);
                case OpType.Ldloc_1: return new Instruction(reader, OpCodes.Ldloc, 1);
                case OpType.Ldloc_2: return new Instruction(reader, OpCodes.Ldloc, 2);
                case OpType.Ldloc_3: return new Instruction(reader, OpCodes.Ldloc, 3);
                case OpType.Ldarga_s: return new Instruction(reader, OpCodes.Ldarga, Operand);
                case OpType.Ldarg_s: return new Instruction(reader, OpCodes.Ldarg, Operand);
                case OpType.Ldarg_0: return new Instruction(reader, OpCodes.Ldarg, 0);
                case OpType.Ldarg_1: return new Instruction(reader, OpCodes.Ldarg, 1);
                case OpType.Ldarg_2: return new Instruction(reader, OpCodes.Ldarg, 2);
                case OpType.Ldarg_3: return new Instruction(reader, OpCodes.Ldarg, 3);
                case OpType.Ldc_i4_s: return new Instruction(reader, OpCodes.Ldc_I4, Operand);
                case OpType.Ldc_i4_m1: return new Instruction(reader, OpCodes.Ldc_I4, -1);
                case OpType.Ldc_i4_0: return new Instruction(reader, OpCodes.Ldc_I4, 0);
                case OpType.Ldc_i4_1: return new Instruction(reader, OpCodes.Ldc_I4, 1);
                case OpType.Ldc_i4_2: return new Instruction(reader, OpCodes.Ldc_I4, 2);
                case OpType.Ldc_i4_3: return new Instruction(reader, OpCodes.Ldc_I4, 3);
                case OpType.Ldc_i4_4: return new Instruction(reader, OpCodes.Ldc_I4, 4);
                case OpType.Ldc_i4_5: return new Instruction(reader, OpCodes.Ldc_I4, 5);
                case OpType.Ldc_i4_6: return new Instruction(reader, OpCodes.Ldc_I4, 6);
                case OpType.Ldc_i4_7: return new Instruction(reader, OpCodes.Ldc_I4, 7);
                case OpType.Ldc_i4_8: return new Instruction(reader, OpCodes.Ldc_I4, 8);
                case OpType.Leave_s:  return new Instruction(reader, OpCodes.Leave, Operand);
                case OpType.Brfalse_s:return new Instruction(reader, OpCodes.Brfalse, Operand);
                case OpType.Brtrue_s: return new Instruction(reader, OpCodes.Brtrue, Operand);
                case OpType.Br_s:     return new Instruction(reader, OpCodes.Br, Operand);
                case OpType.Beq_s:    return new Instruction(reader, OpCodes.Beq, Operand);
                case OpType.Bge_s:    return new Instruction(reader, OpCodes.Bge, Operand);
                case OpType.Bge_un_s: return new Instruction(reader, OpCodes.Bgt_Un, Operand);
                case OpType.Ble_s:    return new Instruction(reader, OpCodes.Ble, Operand);
                case OpType.Ble_un_s: return new Instruction(reader, OpCodes.Ble_Un, Operand);
                case OpType.Blt_s:    return new Instruction(reader, OpCodes.Blt, Operand);
                case OpType.Blt_un_s: return new Instruction(reader, OpCodes.Blt_Un, Operand);
                case OpType.Bne_un_s: return new Instruction(reader, OpCodes.Bne_Un, Operand);
                default:              return this;
            }
        }

        /// <summary>
        /// Resolves a member token.
        /// </summary>
        /// <returns>The method data.</returns>
        public MemberInfo ResolveMember()
        {
            if (OpCode.OperandType != OperandType.InlineTok)
                throw new InvalidOperationException("Instruction does not reference a member.");
            return reader.ResolveMethod(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a field token.
        /// </summary>
        /// <returns>The field data.</returns>
        public FieldInfo ResolveField()
        {
            if (OpCode.OperandType != OperandType.InlineField && OpCode.OperandType != OperandType.InlineTok)
                throw new InvalidOperationException("Instruction does not reference a field.");
            return reader.ResolveField(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a method token.
        /// </summary>
        /// <returns>The method data.</returns>
        public MethodBase ResolveMethod()
        {
            if (OpCode.OperandType != OperandType.InlineMethod && OpCode.OperandType != OperandType.InlineTok)
                throw new InvalidOperationException("Instruction does not reference a method.");
            return reader.ResolveMethod(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a string token.
        /// </summary>
        /// <returns>The string value.</returns>
        public string ResolveString()
        {
            if (OpCode.OperandType != OperandType.InlineString)
                throw new InvalidOperationException("Instruction does not reference a string.");
            return reader.ResolveString(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a type token.
        /// </summary>
        /// <returns>The type data.</returns>
        public Type ResolveType()
        {
            if (OpCode.OperandType != OperandType.InlineType && OpCode.OperandType != OperandType.InlineTok)
                throw new InvalidOperationException("Instruction does not reference a type or token.");
            return reader.ResolveType(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a type token.
        /// </summary>
        /// <returns>The type data.</returns>
        public byte[] ResolveSignature()
        {
            if (OpCode.OperandType != OperandType.InlineSig)
                throw new InvalidOperationException("Instruction does not reference a signature.");
            return reader.ResolveSignature(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a local variable.
        /// </summary>
        /// <returns>The local variable referenced by this instruction.</returns>
        public LocalVariableInfo ResolveLocal()
        {
            switch (OpCode.OperandType)
            {
                case OperandType.InlineVar:
                    return reader.Locals[Operand.Int16];
                case OperandType.ShortInlineVar:
                    return reader.Locals[Operand.Int8];
                default:
                    throw new InvalidOperationException("Instruction does not reference a local.");
            }
        }

        /// <summary>
        /// Resolve the set of branches in a given switch statement.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<int, ILReader.Label>> ResolveBranches()
        {
            return reader.ResolveBranches(Operand.Int32);
        }

        /// <summary>
        /// Returns a string representation of the instruction.
        /// </summary>
        /// <returns>A string representation of the instruction.</returns>
        public override string ToString()
        {
            return OpCode.Name + " " + Resolve();
        }
    }
}
