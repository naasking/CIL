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
        /// <remarks>Use Module.Resolve* to extract the string, fields, methods, or other values.</remarks>
        [FieldOffset(0)] public readonly int MetadataToken;

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
        /// The Module containing this instruction, used primarily to resolve referants.
        /// </summary>
        public readonly Module Module;

        /// <summary>
        /// Construct an instruction.
        /// </summary>
        /// <param name="module">The module used to resolve referants.</param>
        /// <param name="op">The instruction opcode.</param>
        /// <param name="arg">The instruction operand.</param>
        public Instruction(Module module, OpCode op, Operand arg)
        {
            this.Module = module;
            this.OpCode = op;
            this.Operand = arg;
        }

        /// <summary>
        /// Construct an instruction.
        /// </summary>
        /// <param name="module">The module used to resolve referants.</param>
        /// <param name="op">The instruction opcode.</param>
        public Instruction(Module module, OpCode op)
            : this(module, op, default(Operand))
        {
        }

        /// <summary>
        /// The module used to resolve referants.
        /// </summary>
        /// <returns></returns>
        public Module GetModule()
        {
            //return Assembly.GetCallingAssembly().ManifestModule;
            return Module;
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
                case OperandType.InlineField: return GetModule().ResolveField(Operand.MetadataToken);
                case OperandType.InlineMethod: return GetModule().ResolveMethod(Operand.MetadataToken);
                case OperandType.InlineSig: return GetModule().ResolveSignature(Operand.MetadataToken);
                case OperandType.InlineString: return GetModule().ResolveString(Operand.MetadataToken);
                case OperandType.InlineTok: return GetModule().ResolveType(Operand.MetadataToken);
                case OperandType.InlineType: return GetModule().ResolveType(Operand.MetadataToken);
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
        public Instruction Simplify()
        {
            switch (OpCode.Type())
            {
                case OpType.Stloc_s: return new Instruction(Module, OpCodes.Stloc, Operand);
                case OpType.Stloc_0: return new Instruction(Module, OpCodes.Stloc, 0);
                case OpType.Stloc_1: return new Instruction(Module, OpCodes.Stloc, 1);
                case OpType.Stloc_2: return new Instruction(Module, OpCodes.Stloc, 2);
                case OpType.Stloc_3: return new Instruction(Module, OpCodes.Stloc, 3);
                case OpType.Ldloc_s: return new Instruction(Module, OpCodes.Ldloc, Operand);
                case OpType.Ldloc_0: return new Instruction(Module, OpCodes.Ldloc, 0);
                case OpType.Ldloc_1: return new Instruction(Module, OpCodes.Ldloc, 1);
                case OpType.Ldloc_2: return new Instruction(Module, OpCodes.Ldloc, 2);
                case OpType.Ldloc_3: return new Instruction(Module, OpCodes.Ldloc, 3);
                case OpType.Ldarg_s: return new Instruction(Module, OpCodes.Ldarg, Operand);
                case OpType.Ldarg_0: return new Instruction(Module, OpCodes.Ldarg, 0);
                case OpType.Ldarg_1: return new Instruction(Module, OpCodes.Ldarg, 1);
                case OpType.Ldarg_2: return new Instruction(Module, OpCodes.Ldarg, 2);
                case OpType.Ldarg_3: return new Instruction(Module, OpCodes.Ldarg, 3);
                case OpType.Ldc_i4_s: return new Instruction(Module, OpCodes.Ldc_I4, Operand);
                case OpType.Ldc_i4_0: return new Instruction(Module, OpCodes.Ldc_I4, 0);
                case OpType.Ldc_i4_1: return new Instruction(Module, OpCodes.Ldc_I4, 1);
                case OpType.Ldc_i4_2: return new Instruction(Module, OpCodes.Ldc_I4, 2);
                case OpType.Ldc_i4_3: return new Instruction(Module, OpCodes.Ldc_I4, 3);
                case OpType.Ldc_i4_4: return new Instruction(Module, OpCodes.Ldc_I4, 4);
                case OpType.Ldc_i4_5: return new Instruction(Module, OpCodes.Ldc_I4, 5);
                case OpType.Ldc_i4_6: return new Instruction(Module, OpCodes.Ldc_I4, 6);
                case OpType.Ldc_i4_7: return new Instruction(Module, OpCodes.Ldc_I4, 7);
                case OpType.Ldc_i4_8: return new Instruction(Module, OpCodes.Ldc_I4, 8);
                case OpType.Leave_s:  return new Instruction(Module, OpCodes.Leave, Operand);
                case OpType.Brfalse_s:return new Instruction(Module, OpCodes.Brfalse, Operand);
                case OpType.Brtrue_s: return new Instruction(Module, OpCodes.Brtrue, Operand);
                case OpType.Br_s:     return new Instruction(Module, OpCodes.Br, Operand);
                case OpType.Beq_s:    return new Instruction(Module, OpCodes.Beq, Operand);
                case OpType.Bge_s:    return new Instruction(Module, OpCodes.Bge, Operand);
                case OpType.Bge_un_s: return new Instruction(Module, OpCodes.Bgt_Un, Operand);
                case OpType.Ble_s:    return new Instruction(Module, OpCodes.Ble, Operand);
                case OpType.Ble_un_s: return new Instruction(Module, OpCodes.Ble_Un, Operand);
                case OpType.Blt_s:    return new Instruction(Module, OpCodes.Blt, Operand);
                case OpType.Blt_un_s: return new Instruction(Module, OpCodes.Blt_Un, Operand);
                case OpType.Bne_un_s: return new Instruction(Module, OpCodes.Bne_Un, Operand);
                default:              return this;
            }
        }

        /// <summary>
        /// Resolves a field token.
        /// </summary>
        /// <returns>The field data.</returns>
        public FieldInfo ResolveField()
        {
            if (OpCode.OperandType != OperandType.InlineField)
                throw new InvalidOperationException("Instruction does not reference a field.");
            return GetModule().ResolveField(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a method token.
        /// </summary>
        /// <returns>The method data.</returns>
        public MethodBase ResolveMethod()
        {
            if (OpCode.OperandType != OperandType.InlineMethod)
                throw new InvalidOperationException("Instruction does not reference a method.");
            return GetModule().ResolveMethod(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a string token.
        /// </summary>
        /// <returns>The string value.</returns>
        public string ResolveString()
        {
            if (OpCode.OperandType != OperandType.InlineString)
                throw new InvalidOperationException("Instruction does not reference a string.");
            return GetModule().ResolveString(Operand.MetadataToken);
        }

        /// <summary>
        /// Resolves a type token.
        /// </summary>
        /// <returns>The type data.</returns>
        public Type ResolveType()
        {
            if (OpCode.OperandType != OperandType.InlineType || OpCode.OperandType != OperandType.InlineTok)
                throw new InvalidOperationException("Instruction does not reference a type or token.");
            return GetModule().ResolveType(Operand.MetadataToken);
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
