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
        [FieldOffset(0)] public readonly float Float32;
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
        [FieldOffset(0)] public readonly IL.Label Label;

        /// <summary>
        /// The operand value.
        /// </summary>
        /// <remarks>Use Module.Resolve* to extract the string, fields, methods, or other values.</remarks>
        [FieldOffset(0)] public readonly int MetadataToken;

        /// <summary>
        /// Construct an operand from a value.
        /// </summary>
        /// <param name="x">The operand value.</param>
        public Operand(IL.Label x)
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
    /// For marking instructions during depth-first search in control flow analysis.
    /// </summary>
    enum DfsColour : byte
    {
        /// <summary>
        /// Unvisited.
        /// </summary>
        White = 0,
        /// <summary>
        /// On stack.
        /// </summary>
        Grey = 1,
        /// <summary>
        /// Done
        /// </summary>
        Black = 2,
    }

    /// <summary>
    /// Flags for control flow analysis of instructions.
    /// </summary>
    [Flags]
    public enum InstructionFlags : byte
    {
        None = 0,
        Leader = 1,
        LoopHeader = 2,
        BreakTarget = 4,
        TryStart = 8,
        HandlerStart = 16,
        FilterStart = 32,
    }

    /// <summary>
    /// A decoded exception handling region (try/catch/finally/fault/filter).
    /// </summary>
    public struct ExceptionRegion
    {
        /// <summary>The type of handler.</summary>
        public readonly ExceptionHandlingClauseOptions Flags;
        /// <summary>Start of the try block.</summary>
        public readonly IL.Label TryStart;
        /// <summary>Exclusive end of the try block.</summary>
        public readonly IL.Label TryEnd;
        /// <summary>Start of the handler block.</summary>
        public readonly IL.Label HandlerStart;
        /// <summary>Exclusive end of the handler block.</summary>
        public readonly IL.Label HandlerEnd;
        /// <summary>Start of the filter block (only valid when Flags is Filter).</summary>
        public readonly IL.Label FilterStart;
        /// <summary>The caught exception type (only valid when Flags is Clause).</summary>
        public readonly Type CatchType;

        internal ExceptionRegion(
            ExceptionHandlingClauseOptions flags,
            IL.Label tryStart, IL.Label tryEnd,
            IL.Label handlerStart, IL.Label handlerEnd,
            IL.Label filterStart, Type catchType)
        {
            Flags = flags;
            TryStart = tryStart;
            TryEnd = tryEnd;
            HandlerStart = handlerStart;
            HandlerEnd = handlerEnd;
            FilterStart = filterStart;
            CatchType = catchType;
        }

        /// <summary>Whether this is a catch clause.</summary>
        public bool IsCatch => Flags == ExceptionHandlingClauseOptions.Clause;
        /// <summary>Whether this is a finally clause.</summary>
        public bool IsFinally => Flags == ExceptionHandlingClauseOptions.Finally;
        /// <summary>Whether this is a fault clause.</summary>
        public bool IsFault => Flags == ExceptionHandlingClauseOptions.Fault;
        /// <summary>Whether this is a filter clause.</summary>
        public bool IsFilter => Flags == ExceptionHandlingClauseOptions.Filter;

        /// <inheritdoc/>
        public override string ToString()
        {
            var kind = IsCatch   ? $"catch({CatchType?.Name})" :
                       IsFinally ? "finally" :
                       IsFault   ? "fault" :
                                   "filter";
            return $".try {TryStart}-{TryEnd} {kind} {HandlerStart}-{HandlerEnd}";
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
        /// <remarks>
        /// This is a little hacky, but it allows instructions to resolve their operands
        /// without needing to store the operand metadata in the instruction itself, which would
        /// require a union of int and other operand types and make the struct much larger. Since
        /// instructions are immutable, we can just store a reference to the reader that created
        /// them and call reader.Resolve*(Operand.MetadataToken) to get the operand value when
        /// needed.
        /// </remarks>
        readonly ILReader reader;

        //--- flags, colour and label should all fit in an Int32 ---//

        /// <summary>
        /// CFA flags for this instruction (basic block leader, loop header, break target).
        /// </summary>
        /// <remarks>
        /// Set via methods in Code that directly index the instruction array,
        /// ensuring the struct is modified in-place rather than copied.
        /// </remarks>
        internal InstructionFlags flags;

        /// <summary>
        /// Colour used to mark instructions during depth-first search in control flow analysis.
        /// </summary>
        internal DfsColour colour;

        /// <summary>
        /// The label for this instruction corresponding to its offset in the bytecode.
        /// </summary>
        internal IL.Label label;

        /// <summary>
        /// Construct an instruction.
        /// </summary>
        /// <param name="reader">The reader that created this instruction.</param>
        /// <param name="op">The instruction opcode.</param>
        /// <param name="arg">The instruction operand.</param>
        /// <param name="label">The instruction's address in the bytecode.</param>
        public Instruction(ILReader reader, OpCode op, Operand arg, IL.Label label)
        {
            this.reader = reader;
            this.OpCode = op;
            this.Operand = arg;
            this.flags = InstructionFlags.None;
            this.colour = DfsColour.White;
            this.label = label;
        }

        internal bool IsInitialized => reader != null;

        /// <summary>
        /// Whether this instruction is a basic block leader.
        /// </summary>
        public bool IsLeader => (flags & InstructionFlags.Leader) != 0;

        /// <summary>
        /// Whether this instruction is a loop header (target of a back-edge).
        /// </summary>
        public bool IsLoopHeader => (flags & InstructionFlags.LoopHeader) != 0;

        /// <summary>
        /// Whether this instruction is a break target (exit from a loop).
        /// </summary>
        public bool IsBreakTarget => (flags & InstructionFlags.BreakTarget) != 0;

        /// <summary>
        /// Whether this instruction begins a try block.
        /// </summary>
        public bool IsTryStart => (flags & InstructionFlags.TryStart) != 0;

        /// <summary>
        /// Whether this instruction begins an exception handler (catch/finally/fault).
        /// </summary>
        public bool IsHandlerStart => (flags & InstructionFlags.HandlerStart) != 0;

        /// <summary>
        /// Whether this instruction begins a filter evaluation block.
        /// </summary>
        public bool IsFilterStart => (flags & InstructionFlags.FilterStart) != 0;

        /// <summary>
        /// Mark the colour.
        /// </summary>
        /// <param name="colour"></param>
        internal void Mark(DfsColour colour) =>
            this.colour = colour;

        /// <summary>
        /// The instruction's size.
        /// </summary>
        public int Size => OpCode.Size + reader.OperandSize(OpCode.OperandType, Operand.MetadataToken);

        // Right now the bytecode exactly matches the output from ildasm in a roundabout way by decoding
        // the operand data twice. I could simplify by removing Intsruction.Operand and replacing with an
        // Int32 operandMetadata field. Depending on OpCode.OperandType, it will call a
        // reader.Resolve*(operandMetadata) method, which will return an Operand value or other operand info.
        // So accessing operands is a little slower, but it's uniform across switches, inline values, etc.
        // and the size of the Instruction struct is smaller since we don't need to accomodate double alongside
        // int in the instruction itself.
        //int operandMetadata;
        //public Operand Operand2 => reader.ResolveInt32(operandMetadata);

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
                case OperandType.InlineI: return Operand.Int32;
                case OperandType.InlineBrTarget: return Operand.Label; //FIXME: is this correct?
                case OperandType.InlineSwitch: return Operand.MetadataToken;
                case OperandType.InlineField: return ResolveField();
                case OperandType.InlineMethod: return ResolveMethod();
                case OperandType.InlineSig: return ResolveSignature();
                case OperandType.InlineString: return ResolveString();
                case OperandType.InlineTok: return ResolveMember();
                case OperandType.InlineType: return ResolveType();
                case OperandType.InlineVar: return Operand.Int16;
                case OperandType.ShortInlineBrTarget: return Operand.Label; //FIXME: is this correct?
                case OperandType.ShortInlineI: return Operand.Int8;
                case OperandType.ShortInlineVar: return Operand.Int8;
                default: //throw new ArgumentException("Unknown operand type.");
                    return "";
            }
        }

        /// <summary>
        /// Convert to a new opcode and operand.
        /// </summary>
        /// <param name="op"></param>
        /// <param name="operand"></param>
        /// <returns></returns>
        Instruction With(OpCode op, Operand operand) =>
            new Instruction(reader, op, operand, label);

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
            //a future simplification might also expand redundant instructions on smaller
            //types to the comparable type with the largest size, ie. _R4=>_R8, _I/_I1=>_I8, etc.
            //case OpType.Ldind_i1: return With(OpCodes.Ldind_I8, Operand);
            //case OpType.Ldind_u1: return With(OpCodes.Ldind_U8, Operand);
            //...
            //case OpType.Stelem_i1: return With(OpCodes.Stelem_I8, Operand);
            //case OpType.Stelem_i2: return With(OpCodes.Stelem_I8, Operand);
            //case OpType.Stelem_i4: return With(OpCodes.Stelem_I8, Operand);
            //...
            switch (OpCode.Type())
            {
                case OpType.Starg_s: return With(OpCodes.Starg, new Operand((short)Operand.Int8));
                case OpType.Stloc_s: return With(OpCodes.Stloc, new Operand((short)Operand.Int8));
                case OpType.Stloc_0: return With(OpCodes.Stloc, 0);
                case OpType.Stloc_1: return With(OpCodes.Stloc, 1);
                case OpType.Stloc_2: return With(OpCodes.Stloc, 2);
                case OpType.Stloc_3: return With(OpCodes.Stloc, 3);
                case OpType.Ldloca_s: return With(OpCodes.Ldloca, new Operand((short)Operand.Int8));
                case OpType.Ldloc_s: return With(OpCodes.Ldloc, new Operand((short)Operand.Int8));
                case OpType.Ldloc_0: return With(OpCodes.Ldloc, 0);
                case OpType.Ldloc_1: return With(OpCodes.Ldloc, 1);
                case OpType.Ldloc_2: return With(OpCodes.Ldloc, 2);
                case OpType.Ldloc_3: return With(OpCodes.Ldloc, 3);
                case OpType.Ldarga_s: return With(OpCodes.Ldarga, new Operand((short)Operand.Int8));
                case OpType.Ldarg_s: return With(OpCodes.Ldarg, new Operand((short)Operand.Int8));
                case OpType.Ldarg_0: return With(OpCodes.Ldarg, 0);
                case OpType.Ldarg_1: return With(OpCodes.Ldarg, 1);
                case OpType.Ldarg_2: return With(OpCodes.Ldarg, 2);
                case OpType.Ldarg_3: return With(OpCodes.Ldarg, 3);
                case OpType.Ldc_i4_s: return With(OpCodes.Ldc_I4, new Operand((short)Operand.Int8));
                case OpType.Ldc_i4_m1: return With(OpCodes.Ldc_I4, -1);
                case OpType.Ldc_i4_0: return With(OpCodes.Ldc_I4, 0);
                case OpType.Ldc_i4_1: return With(OpCodes.Ldc_I4, 1);
                case OpType.Ldc_i4_2: return With(OpCodes.Ldc_I4, 2);
                case OpType.Ldc_i4_3: return With(OpCodes.Ldc_I4, 3);
                case OpType.Ldc_i4_4: return With(OpCodes.Ldc_I4, 4);
                case OpType.Ldc_i4_5: return With(OpCodes.Ldc_I4, 5);
                case OpType.Ldc_i4_6: return With(OpCodes.Ldc_I4, 6);
                case OpType.Ldc_i4_7: return With(OpCodes.Ldc_I4, 7);
                case OpType.Ldc_i4_8: return With(OpCodes.Ldc_I4, 8);
                case OpType.Leave_s: return With(OpCodes.Leave, Operand);
                case OpType.Brfalse_s: return With(OpCodes.Brfalse, Operand);
                case OpType.Brtrue_s: return With(OpCodes.Brtrue, Operand);
                case OpType.Br_s: return With(OpCodes.Br, Operand);
                case OpType.Beq_s: return With(OpCodes.Beq, Operand);
                case OpType.Bge_s: return With(OpCodes.Bge, Operand);
                case OpType.Bge_un_s: return With(OpCodes.Bge_Un, Operand);
                case OpType.Bgt_s: return With(OpCodes.Bgt, Operand);
                case OpType.Bgt_un_s: return With(OpCodes.Bgt_Un, Operand);
                case OpType.Ble_s: return With(OpCodes.Ble, Operand);
                case OpType.Ble_un_s: return With(OpCodes.Ble_Un, Operand);
                case OpType.Blt_s: return With(OpCodes.Blt, Operand);
                case OpType.Blt_un_s: return With(OpCodes.Blt_Un, Operand);
                case OpType.Bne_un_s: return With(OpCodes.Bne_Un, Operand);
                default: return this;
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
        /// <returns>A sequence of offset and branch target pairs.</returns>
        public IEnumerable<KeyValuePair<int, IL.Label>> ResolveBranches()
        {
            return reader.ResolveBranches(Operand.Int32);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var suffix = IsLoopHeader ? " <- Loop" : IsBreakTarget ? " <- Break" : "";
            return OpCode.Name + " " + Resolve() + suffix;
        }
    }
}
