using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;

namespace CIL
{
    /// <summary>
    /// Methods for parsing and processing CIL opcodes.
    /// </summary>
    public static partial class IL
    {
        static readonly OpCode[] oneByteOps = new OpCode[256];
        static readonly OpCode[] twoByteOps = new OpCode[256];
        static readonly MethodInfo r8IsInfinity = typeof(double).GetMethod("IsInfinity", BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo r4IsInfinity = typeof(float).GetMethod("IsInfinity", BindingFlags.Static | BindingFlags.Public);
        //static readonly MethodInfo blockCopy = new Action<Array, Int32, Array, Int32, Int32>(Buffer.BlockCopy).Method;
        static readonly ConstructorInfo arithError = typeof(ArithmeticException).GetConstructor(new[] { typeof(string) });
        static readonly Type nativeInt = IntPtr.Size == 8 ? typeof(long) : typeof(int);

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
        /// A position in the instruction stream.
        /// </summary>
        public struct Label : IEquatable<Label>, IComparable<Label>
        {
            /// <summary>
            /// Create a label for the given offste
            /// </summary>
            /// <param name="offset"></param>
            internal Label(int offset)
            {
                if (offset < 0)
                    throw new ArgumentException("Invalid instruction offset.");
                this.offset = (ushort)offset;
            }
            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return obj is Label && Equals((Label)obj);
            }
            /// <inheritdoc/>
            public override int GetHashCode()
            {
                return Offset;
            }
            /// <inheritdoc/>
            public bool Equals(Label other)
            {
                return this == other;
            }
            /// <inheritdoc/>
            public int CompareTo(Label other)
            {
                return Offset.CompareTo(other.Offset);
            }
            /// <inheritdoc/>
            public override string ToString()
            {
                return "IL_" + Offset.ToString("X4");
            }

            /// <summary>
            /// Max CIL method size is 65535 bytes, so ushort is sufficient to store instruction offsets.
            /// </summary>
            ushort offset;

            /// <summary>
            /// The offset of the instruction in the bytecode stream.
            /// </summary>
            public int Offset
            {
                get => offset;
                set => offset = (ushort)value;
            }

            /// <inheritdoc/>
            public static int operator -(Label left, Label right)
            {
                return left.Offset - right.Offset;
            }
            /// <inheritdoc/>
            public static bool operator <(Label left, Label right)
            {
                return left.Offset < right.Offset;
            }
            /// <inheritdoc/>
            public static bool operator >(Label left, Label right)
            {
                return left.Offset > right.Offset;
            }
            /// <inheritdoc/>
            public static bool operator <=(Label left, Label right)
            {
                return left.Offset <= right.Offset;
            }
            /// <inheritdoc/>
            public static bool operator >=(Label left, Label right)
            {
                return left.Offset >= right.Offset;
            }
            /// <inheritdoc/>
            public static bool operator ==(Label left, Label right)
            {
                return left.Offset == right.Offset;
            }
            /// <inheritdoc/>
            public static bool operator !=(Label left, Label right)
            {
                return left.Offset != right.Offset;
            }
        }

        /// <summary>
        /// Get the OpCode of the corresponding byte.
        /// </summary>
        /// <param name="ops">The bytecode to analyze.</param>
        /// <param name="index">The index into the bytecode.</param>
        /// <returns>The <see cref="OpCode"/> at the given index of the bytecode stream.</returns>
        public static OpCode GetOpCode(byte[] ops, ref int index)
        {
            var o = ops[index++];
            return o == 0xFE ? twoByteOps[ops[index++]] : oneByteOps[o];
        }

        /// <summary>
        /// The instruction type of the opcode.
        /// </summary>
        /// <param name="op">The opcode.</param>
        /// <returns>The opcode type.</returns>
        public static OpType Type(this OpCode op)
        {
            return (OpType)op.Value;
        }

        /// <summary>
        /// Check if the given opcode is a branching instruction.
        /// </summary>
        /// <param name="op">The opcode.</param>
        /// <returns>True if the opcode is a branch instruction.</returns>
        public static bool IsBranch(this OpCode op) 
        {
            switch (op.FlowControl)
            {
                case FlowControl.Branch:
                case FlowControl.Cond_Branch:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Get the instructions from the given method.
        /// </summary>
        /// <param name="code">The method to analyze.</param>
        /// <returns>An <seealso cref="ILReader"/> that iterates over the bytecode instructions.</returns>
        public static ILReader GetILReader(this MethodBase code)
        {
            return code.GetILReader(out var byteCodeLength);
        }

        /// <summary>
        /// Get the instructions from the given method.
        /// </summary>
        /// <param name="code">The method to analyze.</param>
        /// <returns>An <seealso cref="ILReader"/> that iterates over the bytecode instructions.</returns>
        public static ILReader GetILReader(this MethodBase code, out int byteCodeLength)
        {
            return new ILReader(code, out byteCodeLength);
        }

        /// <summary>
        /// Get the instructions from the given method.
        /// </summary>
        /// <param name="code">The method to analyze.</param>
        /// <returns>A stream of instructions.</returns>
        public static Code GetInstructions(this MethodBase code)
        {
            var il = code.GetILReader(out var byteCodeLength);
            var bc = Decode(il, new Code(byteCodeLength), 0);
            bc.SetExceptionHandlers(il.ExceptionHandlers);
            return CFA(bc);
        }

        /// <summary>
        /// Decompile a method using the specified decompiler.
        /// </summary>
        /// <typeparam name="T">The type of decompiled expressions.</typeparam>
        /// <param name="code">The method to decompile.</param>
        /// <param name="decompiler">The CIL expression decompiler.</param>
        /// <param name="args">The method arguments.</param>
        /// <returns>An expression representing the code.</returns>
        public static T Decompile<T>(this MethodBase code, IExpression<T> decompiler, out T[] args)
        {
            var il = code.GetILReader(out var byteCodeLength);
            var ps = code.GetParameters();
            var argCount = (code.IsStatic ? 0 : 1) + ps.Length;
            args = new T[argCount];
            var argTypes = new Type[argCount];
            var argIsAddress = new bool[argCount];
            int ai = 0;
            if (!code.IsStatic)
            {
                args[ai] = decompiler.Param("this", code.DeclaringType);
                argTypes[ai] = code.DeclaringType;
                argIsAddress[ai] = code.DeclaringType.IsValueType;
                ++ai;
            }
            for (int k = 0; k < ps.Length; ++k, ++ai)
            {
                args[ai] = decompiler.Param(ps[k]);
                var pt = ps[k].ParameterType;
                argIsAddress[ai] = pt.IsByRef;
                argTypes[ai] = pt.IsByRef ? pt.GetElementType() : pt;
            }

            var ilLocals = il.Locals;
            var locals = new T[ilLocals.Count];
            var localTypes = new Type[ilLocals.Count];
            var localIsAddress = new bool[ilLocals.Count];
            for (int k = 0; k < ilLocals.Count; ++k)
            {
                locals[k] = decompiler.Local(ilLocals[k]);
                var lt = ilLocals[k].LocalType;
                localIsAddress[k] = lt.IsByRef;
                localTypes[k] = lt.IsByRef ? lt.GetElementType() : lt;
            }

            var bc = Decode(il, new Code(byteCodeLength), 0);
            bc.SetExceptionHandlers(il.ExceptionHandlers);
            var returnType = (code as MethodInfo)?.ReturnType ?? typeof(void);
            return new Decompiler<T>(bc, byteCodeLength, decompiler, args, argTypes, argIsAddress, locals, localTypes, localIsAddress, returnType).Run();
        }

        /// <summary>
        /// First-pass over the instruction stream decodes all instructions and
        /// tags all loops. A second pass can translate decoded instructions into
        /// high-level expressions.
        /// </summary>
        static Code Decode(ILReader il, Code bc, int offset)
        {
            while (il.MoveNext())
            {
                var size = bc.Add(offset, ref il.current);
                offset += size;
            }
            return bc;
        }

        static Code CFA(Code code)
        {
            if (code.Count == 0) return code;

            // Pass 1: identify basic block leaders (flags stored directly on instructions)
            code.SetFlag(0, InstructionFlags.Leader);
            for (int i = 0; i < code.Count; ++i)
            {
                ref var instr = ref code[i];
                switch (instr.OpCode.FlowControl)
                {
                    case FlowControl.Branch:
                        code.SetFlag(code.GetIndex(instr.Operand.Label), InstructionFlags.Leader);
                        if (i + 1 < code.Count) code.SetFlag(i + 1, InstructionFlags.Leader);
                        break;
                    case FlowControl.Cond_Branch:
                        if (instr.OpCode.OperandType == OperandType.InlineSwitch)
                            foreach (var kv in instr.ResolveBranches())
                                code.SetFlag(code.GetIndex(kv.Value), InstructionFlags.Leader);
                        else
                            code.SetFlag(code.GetIndex(instr.Operand.Label), InstructionFlags.Leader);
                        if (i + 1 < code.Count) code.SetFlag(i + 1, InstructionFlags.Leader);
                        break;
                    case FlowControl.Return:
                    case FlowControl.Throw:
                        if (i + 1 < code.Count) code.SetFlag(i + 1, InstructionFlags.Leader);
                        break;
                }
            }

            // Pass 2: DFS over the control flow graph with 3-color marking
            VisitBlock(code, 0);

            // Pass 3: visit exception handler entry points not reachable from normal flow
            if (code.ExceptionHandlers != null)
            {
                foreach (var handler in code.ExceptionHandlers)
                {
                    var handlerIdx = code.GetIndex(handler.HandlerStart);
                    if (code[handlerIdx].colour == DfsColour.White)
                        VisitBlock(code, handlerIdx);
                    if (handler.IsFilter)
                    {
                        var filterIdx = code.GetIndex(handler.FilterStart);
                        if (code[filterIdx].colour == DfsColour.White)
                            VisitBlock(code, filterIdx);
                    }
                }
            }

            return code;
        }

        static int BlockEnd(Code code, int start)
        {
            int end = start;
            for (int j = start + 1; j < code.Count && !code[j].IsLeader; ++j)
                end = j;
            return end;
        }

        static void VisitBlock(Code code, int start)
        {
            code[start].Mark(DfsColour.Grey);
            var end = BlockEnd(code, start);
            ref var last = ref code[end];

            switch (last.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                    VisitEdge(code, end, last.Operand.Label);
                    break;
                case FlowControl.Cond_Branch:
                    if (last.OpCode.OperandType == OperandType.InlineSwitch)
                        foreach (var kv in last.ResolveBranches())
                            VisitEdge(code, end, kv.Value);
                    else
                        VisitEdge(code, end, last.Operand.Label);
                    if (end + 1 < code.Count)
                        VisitEdge(code, end, code.GetLabel(end + 1));
                    break;
                case FlowControl.Return:
                case FlowControl.Throw:
                    break;
                default:
                    if (end + 1 < code.Count)
                        VisitEdge(code, end, code.GetLabel(end + 1));
                    break;
            }
            code[start].Mark(DfsColour.Black);
        }

        static void VisitEdge(Code code, int sourceIdx, Label target)
        {
            var targetIdx = code.GetIndex(target);
            switch (code[targetIdx].colour)
            {
                case DfsColour.Grey: // Grey -> back-edge -> loop
                    code.AddLoop(target);
                    DetectBreaks(code, targetIdx, sourceIdx);
                    break;
                case DfsColour.White: // White -> tree edge -> recurse
                    VisitBlock(code, targetIdx);
                    break;
                // Black -> cross/forward edge -> already explored, skip
            }
        }

        static void DetectBreaks(Code code, int loopStart, int loopEnd)
        {
            for (int k = loopStart; k <= loopEnd; ++k)
            {
                ref var instr = ref code[k];
                if (instr.OpCode.FlowControl != FlowControl.Branch &&
                    instr.OpCode.FlowControl != FlowControl.Cond_Branch)
                    continue;
                if (instr.OpCode.OperandType == OperandType.InlineSwitch)
                {
                    foreach (var kv in instr.ResolveBranches())
                    {
                        var exitIdx = code.GetIndex(kv.Value);
                        if (exitIdx < loopStart || exitIdx > loopEnd)
                            code.AddBreak(kv.Value);
                    }
                }
                else
                {
                    var exitIdx = code.GetIndex(instr.Operand.Label);
                    if (exitIdx < loopStart || exitIdx > loopEnd)
                        code.AddBreak(instr.Operand.Label);
                }
            }
        }

        /// <summary>
        /// Translates a decoded CIL method body into structured <see cref="IExpression{T}"/>
        /// expressions using a flat-array CFG, Cooper-Harvey-Kennedy dominators/post-dominators,
        /// arena-backed evaluation frames, and decoder-side CIL type/constant tracking.
        /// </summary>
        sealed class Decompiler<T>
        {
            static readonly MethodInfo r8IsNaN = typeof(double).GetMethod(nameof(double.IsNaN), new[] { typeof(double) });
            static readonly MethodInfo r4IsNaN = typeof(float).GetMethod(nameof(float.IsNaN), new[] { typeof(float) });
            static readonly MethodInfo getTypeFromHandle = typeof(Type).GetMethod(nameof(System.Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) });
            static readonly Type nativeUInt = IntPtr.Size == 8 ? typeof(ulong) : typeof(uint);
            enum ConstKind : byte { None, Null, Bool, Int32, Int64, Float32, Float64, Ref }

            [Flags]
            enum SlotFlags : byte
            {
                None = 0,
                Stable = 1,
                Address = 2,
                FromCmp = 4,
                Element = 8,
                FieldAddr = 16,
            }

            struct Slot
            {
                public T Value;
                public Type Type;
                public ConstKind Const;
                public Operand ConstVal;
                public object ConstObj;
                public SlotFlags Flags;
                public T AddrBase, AddrIndex;
                public Type AddrBaseType, AddrIndexType;
                public FieldInfo AddrField;
                public bool AddrBaseStable, AddrIndexStable;

                public bool IsStable => (Flags & SlotFlags.Stable) != 0;
                public bool IsAddress => (Flags & SlotFlags.Address) != 0;
                public bool IsFromCmp => (Flags & SlotFlags.FromCmp) != 0;
                public bool IsElement => (Flags & SlotFlags.Element) != 0;
                public bool IsFieldAddr => (Flags & SlotFlags.FieldAddr) != 0;
                public bool IsConstant => Const != ConstKind.None;
                public bool IsNull => Const == ConstKind.Null;
            }

            struct Frame
            {
                public int StackBase;
                public int StackTop;
                public int Low;
                public int Clean;
                public int StmtBase;
                public int StmtTop;

                public int Depth => StackTop - StackBase;
                public int StmtCount => StmtTop - StmtBase;
            }

            struct ArmResult
            {
                public int ReachedBlock;   // -1 if terminated (ret/throw/goto/break/continue/leave)
                public int Low;
                public int Depth;
                public bool HasStmts;
                public T Stmts;
                public Slot SingleVal;     // inline storage for the common k == 1 case
                public Slot[] ExtraVals;   // only allocated when k > 1
            }

            enum Cmp : byte { Eq, Ne, Lt, Le, Gt, Ge }

            enum RegionKind : byte { Root, Try, Catch, Finally, Fault, Filter }

            sealed class TryNode
            {
                public int TryStart, TryEnd, End;           // block indices [TryStart, TryEnd), extent [TryStart, End)
                public readonly List<ExceptionRegion> Clauses = new List<ExceptionRegion>();
                public TryNode Parent;
                public List<TryNode> Children;
                public int[] Exits;                         // distinct leave target blocks outside [TryStart, End)
                public int NormalExit = -1;                 // preferred fall-out target block
            }

            /// <summary>
            /// Flat-array dominator/loop analysis for a single region (root method or try/handler body).
            /// </summary>
            sealed class RegionCfg
            {
                public int Entry;                           // entry block ID
                public int Lo, Hi;                          // valid block range [Lo, Hi)
                public int Count;                           // reachable block count
                public int[] RpoOrder;                      // rpoIdx -> blockId
                public int[] RpoIndex;                      // blockId -> rpoIdx (-1 if unreachable)
                public int[] Idom;                          // rpoIdx -> idom rpoIdx
                public int[] DomIn, DomOut;                 // Euler tour on dominator tree (indexed by blockId)
                public int[] Ipdom;                         // blockId -> immediate post-dominator blockId (-1 if none/exit)
                public bool[] IsLoopHeader;                 // blockId -> true if natural loop header
                public int[] LoopHeader;                    // blockId -> innermost loop header blockId (-1 if none)
                public int[] LoopParent;                    // header blockId -> enclosing loop header blockId (-1 if none)
                public int[] LoopExit;                      // header blockId -> primary exit blockId (-1 if none)
                public int[] SuccStart, SuccList;           // region-local successors (blockId -> blockIds)
                public int[] PredStart, PredList;           // region-local predecessors (blockId -> blockIds)
                public int[] PredCount;                     // blockId -> number of region-local predecessors

                public bool Dominates(int a, int b) =>
                    DomIn[a] >= 0 && DomIn[b] >= 0 && DomIn[a] <= DomIn[b] && DomOut[b] <= DomOut[a];

                public bool InLoop(int b, int header)
                {
                    for (int h = LoopHeader[b]; h >= 0; h = LoopParent[h])
                        if (h == header) return true;
                    return false;
                }
            }

            sealed class ActiveRegion
            {
                public ActiveRegion Parent;
                public RegionCfg Cfg;
                public RegionKind Kind;
                public TryNode Try;
                public int Entry;
                public int Stop;
                public int LoopHeader;
                public T BreakLabel, ContinueLabel;
                public bool BreakUsed, ContinueUsed;
                public int LoopExit;
                public List<int> Pending;
            }

            readonly Code code;
            readonly int byteLength;
            readonly int count;
            readonly IExpression<T> exp;
            readonly T[] args, locals;
            readonly Type[] argTypes, localTypes;
            readonly bool[] argIsAddress, localIsAddress;
            readonly bool[] argWritten, localWritten;
            readonly Type returnType;

            // Reusable flat arenas (zero per-branch List allocations)
            Slot[] stackBuf = new Slot[16];
            T[] stmtBuf = new T[32];

            // CFG tables (null on the branchless/exceptionless fast path)
            bool hasBranches, hasExceptions;
            int blockCount;
            int[] blockOf;
            int[] blockStart;
            List<TryNode> rootTries;
            TryNode[] tryAtBlock;

            // Per-block emission state (allocated only when CFG is built)
            bool[] blockEmitted;
            bool[] hasLabel;
            bool[] labelEmitted;
            T[] blockLabels;
            Slot[][] entryTemps;

            int tempCount;
            Type constrainedType;

            public Decompiler(
                Code code, int byteLength, IExpression<T> exp,
                T[] args, Type[] argTypes, bool[] argIsAddress,
                T[] locals, Type[] localTypes, bool[] localIsAddress,
                Type returnType)
            {
                this.code = code;
                this.byteLength = byteLength;
                this.count = code.Count;
                this.exp = exp;
                this.args = args;
                this.argTypes = argTypes;
                this.argIsAddress = argIsAddress;
                this.locals = locals;
                this.localTypes = localTypes;
                this.localIsAddress = localIsAddress;
                this.returnType = returnType;

                argWritten = new bool[args.Length];
                localWritten = new bool[locals.Length];
                hasExceptions = code.ExceptionHandlers != null && code.ExceptionHandlers.Length > 0;

                // Single linear scan: mark written args/locals and detect branches
                for (int j = 0; j < count; ++j)
                {
                    ref var x = ref code[j];
                    var op = x.OpCode.Type();
                    switch (op)
                    {
                        case OpType.Starg:
                        case OpType.Starg_s:
                        case OpType.Ldarga:
                        case OpType.Ldarga_s:
                            argWritten[ArgIndex(x)] = true;
                            break;
                        case OpType.Stloc:
                        case OpType.Stloc_s:
                        case OpType.Stloc_0:
                        case OpType.Stloc_1:
                        case OpType.Stloc_2:
                        case OpType.Stloc_3:
                        case OpType.Ldloca:
                        case OpType.Ldloca_s:
                            localWritten[LocalIndex(x)] = true;
                            break;
                    }
                    var fc = x.OpCode.FlowControl;
                    if (fc == FlowControl.Branch || fc == FlowControl.Cond_Branch)
                        hasBranches = true;
                }
            }

            public T Run()
            {
                if (count == 0)
                    throw new InvalidProgramException("Control falls off the end of the method body.");

                // Fast path: straight-line method with no branches and no exception handlers.
                // Zero CFG / dominator / region allocations.
                if (!hasBranches && !hasExceptions)
                    return RunStraightLine();

                BuildBlocks();
                if (hasExceptions)
                    BuildTryTree();

                blockEmitted = new bool[blockCount];
                hasLabel = new bool[blockCount];
                labelEmitted = new bool[blockCount];
                blockLabels = new T[blockCount];
                entryTemps = new Slot[blockCount][];

                var cfg = BuildRegionCfg(0, 0, blockCount, null);
                var frame = NewFrame(0, 0);
                var root = new ActiveRegion
                {
                    Cfg = cfg,
                    Kind = RegionKind.Root,
                    Entry = 0,
                    Stop = -1,
                    LoopHeader = -1,
                    LoopExit = -1,
                };
                EmitSequence(0, ref frame, root, isEntry: true);
                EmitPendingSiblings(ref frame, root, terminated: true);
                return MaterializeBlock(frame.StmtBase, frame.StmtCount);
            }

            T RunStraightLine()
            {
                var frame = NewFrame(0, 0);
                for (int i = 0; i < count; ++i)
                {
                    if (StepInstruction(i, ref frame, RegionKind.Root, out var term))
                    {
                        if (term)
                            return MaterializeBlock(frame.StmtBase, frame.StmtCount);
                    }
                }
                throw new InvalidProgramException("Control falls off the end of the method body.");
            }

            #region Basic Blocks & Try Tree Construction
            void BuildBlocks()
            {
                var isLeader = new bool[count];
                isLeader[0] = true;
                for (int i = 0; i < count; ++i)
                {
                    ref var x = ref code[i];
                    var fc = x.OpCode.FlowControl;
                    if (fc == FlowControl.Branch)
                    {
                        var t = Target(x);
                        if (t < count) isLeader[t] = true;
                        if (i + 1 < count) isLeader[i + 1] = true;
                    }
                    else if (fc == FlowControl.Cond_Branch)
                    {
                        if (x.OpCode.OperandType == OperandType.InlineSwitch)
                        {
                            foreach (var kv in x.ResolveBranches())
                            {
                                var t = Index(kv.Value);
                                if (t < count) isLeader[t] = true;
                            }
                        }
                        else
                        {
                            var t = Target(x);
                            if (t < count) isLeader[t] = true;
                        }
                        if (i + 1 < count) isLeader[i + 1] = true;
                    }
                    else if (fc == FlowControl.Return || fc == FlowControl.Throw || x.OpCode.Type() == OpType.Jmp)
                    {
                        if (i + 1 < count) isLeader[i + 1] = true;
                    }
                }

                if (hasExceptions)
                {
                    foreach (var h in code.ExceptionHandlers)
                    {
                        MarkLeader(isLeader, Index(h.TryStart));
                        MarkLeader(isLeader, Index(h.TryEnd));
                        MarkLeader(isLeader, Index(h.HandlerStart));
                        MarkLeader(isLeader, Index(h.HandlerEnd));
                        if (h.IsFilter)
                            MarkLeader(isLeader, Index(h.FilterStart));
                    }
                }

                blockOf = new int[count];
                int bCount = 0;
                for (int i = 0; i < count; ++i)
                    if (isLeader[i]) ++bCount;

                blockCount = bCount;
                blockStart = new int[bCount + 1];
                int b = -1;
                for (int i = 0; i < count; ++i)
                {
                    if (isLeader[i])
                    {
                        ++b;
                        blockStart[b] = i;
                    }
                    blockOf[i] = b;
                }
                blockStart[bCount] = count;
            }

            void MarkLeader(bool[] isLeader, int idx)
            {
                if (idx >= 0 && idx < count)
                    isLeader[idx] = true;
            }

            int BlockIndex(IL.Label label)
            {
                var idx = Index(label);
                return idx >= count ? blockCount : blockOf[idx];
            }

            void BuildTryTree()
            {
                // ECMA-335 orders exception clauses innermost-first.
                // Contiguous Catch/Filter clauses sharing (TryStart, TryEnd) form one TryNode;
                // Finally and Fault clauses each form their own wrapping TryNode.
                var nodes = new List<TryNode>();
                TryNode prev = null;
                foreach (var h in code.ExceptionHandlers)
                {
                    int ts = BlockIndex(h.TryStart);
                    int te = BlockIndex(h.TryEnd);
                    int hs = BlockIndex(h.HandlerStart);
                    int he = BlockIndex(h.HandlerEnd);
                    if (hs < te || (h.IsFilter && BlockIndex(h.FilterStart) < te))
                        throw new NotSupportedException("Exception handlers that precede their protected block are not supported.");

                    bool canMerge = prev != null
                        && prev.TryStart == ts && prev.TryEnd == te
                        && (h.IsCatch || h.IsFilter)
                        && (prev.Clauses[0].IsCatch || prev.Clauses[0].IsFilter);

                    if (canMerge)
                    {
                        prev.Clauses.Add(h);
                        if (he > prev.End) prev.End = he;
                    }
                    else
                    {
                        var node = new TryNode { TryStart = ts, TryEnd = te, End = he };
                        node.Clauses.Add(h);
                        nodes.Add(node);
                        prev = node;
                    }
                }

                // Build nesting tree: since clauses are innermost-first, for each node i,
                // its parent is the first subsequent node j > i whose extent [TryStart, End) encloses node i.
                rootTries = new List<TryNode>();
                for (int i = 0; i < nodes.Count; ++i)
                {
                    var n = nodes[i];
                    TryNode parent = null;
                    for (int j = i + 1; j < nodes.Count; ++j)
                    {
                        var cand = nodes[j];
                        if (cand.TryStart <= n.TryStart && n.End <= cand.End)
                        {
                            parent = cand;
                            break;
                        }
                    }
                    n.Parent = parent;
                    if (parent == null)
                        rootTries.Add(n);
                    else
                        (parent.Children ?? (parent.Children = new List<TryNode>())).Add(n);
                }

                // Precompute distinct leave exits for each TryNode (outside its own [TryStart, End))
                var exitList = new List<int>();
                foreach (var n in nodes)
                {
                    exitList.Clear();
                    int loInstr = blockStart[n.TryStart];
                    int hiInstr = blockStart[n.End];
                    for (int j = loInstr; j < hiInstr; ++j)
                    {
                        var op = code[j].OpCode.Type();
                        if (op == OpType.Leave || op == OpType.Leave_s)
                        {
                            var tb = BlockIndex(code[j].Operand.Label);
                            if ((tb < n.TryStart || tb >= n.End) && !exitList.Contains(tb))
                                exitList.Add(tb);
                        }
                    }
                    n.Exits = exitList.ToArray();
                    // Prefer the block immediately following the handler region as normal fall-out if present
                    if (exitList.Count == 1)
                        n.NormalExit = exitList[0];
                    else if (exitList.Contains(n.End))
                        n.NormalExit = n.End;
                    else if (exitList.Count > 0)
                        n.NormalExit = exitList[0];
                }

                // Map TryStart -> outermost TryNode starting there (for quick lookup)
                tryAtBlock = new TryNode[blockCount];
                for (int i = 0; i < rootTries.Count; ++i)
                    tryAtBlock[rootTries[i].TryStart] = rootTries[i];
            }

            /// <summary>
            /// Finds the direct child TryNode of <paramref name="parentTry"/> starting at <paramref name="block"/>,
            /// within the block range [<paramref name="lo"/>, <paramref name="hi"/>).
            /// </summary>
            TryNode FindChildTry(TryNode parentTry, int block, int lo, int hi)
            {
                if (!hasExceptions) return null;
                var list = parentTry == null ? rootTries : parentTry.Children;
                if (list == null) return null;
                // Multiple children can share TryStart if one wraps another (already linked via Parent),
                // so direct children of parentTry at the same TryStart are at most one.
                for (int i = 0; i < list.Count; ++i)
                {
                    var n = list[i];
                    if (n.TryStart == block && n.TryStart >= lo && n.End <= hi)
                        return n;
                }
                return null;
            }
            #endregion

            #region Flat-Array Dominators, Post-Dominators & Natural Loops
            RegionCfg BuildRegionCfg(int entry, int lo, int hi, TryNode currentTry)
            {
                // 1. Build region-local successor lists (treating child TryNodes as single super-nodes)
                var succStart = new int[blockCount + 1];
                var succBuf = new List<int>(blockCount * 2);
                var childTryMap = hasExceptions ? new TryNode[blockCount] : null;
                if (hasExceptions)
                {
                    var list = currentTry == null ? rootTries : currentTry.Children;
                    if (list != null)
                        for (int k = 0; k < list.Count; ++k)
                        {
                            var ch = list[k];
                            if (ch.TryStart >= lo && ch.End <= hi)
                                childTryMap[ch.TryStart] = ch;
                        }
                }

                for (int b = 0; b < blockCount; ++b)
                {
                    succStart[b] = succBuf.Count;
                    if (b < lo || b >= hi) continue;

                    var childTry = childTryMap?[b];
                    if (childTry != null)
                    {
                        for (int k = 0; k < childTry.Exits.Length; ++k)
                        {
                            var t = childTry.Exits[k];
                            if (t >= lo && t < hi)
                                succBuf.Add(t);
                        }
                        continue;
                    }

                    int lastIdx = blockStart[b + 1] - 1;
                    ref var last = ref code[lastIdx];
                    var fc = last.OpCode.FlowControl;
                    if (fc == FlowControl.Branch)
                    {
                        var t = BlockIndex(last.Operand.Label);
                        if (t >= lo && t < hi) succBuf.Add(t);
                    }
                    else if (fc == FlowControl.Cond_Branch)
                    {
                        if (last.OpCode.OperandType == OperandType.InlineSwitch)
                        {
                            foreach (var kv in last.ResolveBranches())
                            {
                                var t = BlockIndex(kv.Value);
                                if (t >= lo && t < hi) AddUniqueSucc(succBuf, succStart[b], t);
                            }
                            if (b + 1 < hi) AddUniqueSucc(succBuf, succStart[b], b + 1);
                        }
                        else
                        {
                            var fall = b + 1;
                            var taken = BlockIndex(last.Operand.Label);
                            if (fall < hi) succBuf.Add(fall);
                            if (taken >= lo && taken < hi && taken != fall) succBuf.Add(taken);
                        }
                    }
                    else if (fc != FlowControl.Return && fc != FlowControl.Throw && last.OpCode.Type() != OpType.Jmp)
                    {
                        if (b + 1 < hi) succBuf.Add(b + 1);
                    }
                }
                succStart[blockCount] = succBuf.Count;
                var succList = succBuf.ToArray();

                // 2. Iterative DFS to compute Reverse Postorder (RPO) of reachable blocks from entry
                var visited = new bool[blockCount];
                var postOrder = new int[blockCount];
                int postCount = 0;
                var dfsNode = new int[blockCount];
                var dfsEdge = new int[blockCount];
                int sp = 0;
                visited[entry] = true;
                dfsNode[0] = entry;
                dfsEdge[0] = succStart[entry];
                ++sp;
                while (sp > 0)
                {
                    int u = dfsNode[sp - 1];
                    int e = dfsEdge[sp - 1];
                    if (e < succStart[u + 1])
                    {
                        dfsEdge[sp - 1] = e + 1;
                        int v = succList[e];
                        if (!visited[v])
                        {
                            visited[v] = true;
                            dfsNode[sp] = v;
                            dfsEdge[sp] = succStart[v];
                            ++sp;
                        }
                    }
                    else
                    {
                        postOrder[postCount++] = u;
                        --sp;
                    }
                }

                int n = postCount;
                var rpoOrder = new int[n];
                var rpoIndex = new int[blockCount];
                for (int b = 0; b < blockCount; ++b) rpoIndex[b] = -1;
                for (int i = 0; i < n; ++i)
                {
                    int b = postOrder[n - 1 - i];
                    rpoOrder[i] = b;
                    rpoIndex[b] = i;
                }

                // 3. Build CSR predecessor lists over reachable blocks only
                var predCount = new int[blockCount];
                for (int i = 0; i < n; ++i)
                {
                    int u = rpoOrder[i];
                    for (int e = succStart[u]; e < succStart[u + 1]; ++e)
                    {
                        int v = succList[e];
                        if (rpoIndex[v] >= 0) ++predCount[v];
                    }
                }
                var predStart = new int[blockCount + 1];
                int pTotal = 0;
                for (int b = 0; b < blockCount; ++b)
                {
                    predStart[b] = pTotal;
                    pTotal += predCount[b];
                }
                predStart[blockCount] = pTotal;
                var predList = new int[pTotal];
                var predPos = new int[blockCount];
                Array.Copy(predStart, predPos, blockCount);
                for (int i = 0; i < n; ++i)
                {
                    int u = rpoOrder[i];
                    for (int e = succStart[u]; e < succStart[u + 1]; ++e)
                    {
                        int v = succList[e];
                        if (rpoIndex[v] >= 0) predList[predPos[v]++] = u;
                    }
                }

                // 4. Cooper-Harvey-Kennedy iterative dominators (indexed by RPO 0..n-1)
                var idom = new int[n];
                for (int i = 0; i < n; ++i) idom[i] = -1;
                idom[0] = 0;
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 1; i < n; ++i)
                    {
                        int b = rpoOrder[i];
                        int newIdom = -1;
                        for (int e = predStart[b]; e < predStart[b + 1]; ++e)
                        {
                            int p = rpoIndex[predList[e]];
                            if (p >= 0 && idom[p] >= 0)
                            {
                                newIdom = newIdom < 0 ? p : IntersectDom(idom, p, newIdom);
                            }
                        }
                        if (newIdom >= 0 && idom[i] != newIdom)
                        {
                            idom[i] = newIdom;
                            changed = true;
                        }
                    }
                }

                // 5. Euler tour on dominator tree for O(1) Dominates(a, b)
                var domFirst = new int[n];
                var domNext = new int[n];
                for (int i = 0; i < n; ++i) domFirst[i] = -1;
                for (int i = n - 1; i >= 1; --i)
                {
                    int p = idom[i];
                    domNext[i] = domFirst[p];
                    domFirst[p] = i;
                }
                var domIn = new int[blockCount];
                var domOut = new int[blockCount];
                for (int b = 0; b < blockCount; ++b) domIn[b] = domOut[b] = -1;
                int timer = 0;
                dfsNode[0] = 0;
                dfsEdge[0] = domFirst[0];
                domIn[rpoOrder[0]] = timer++;
                sp = 1;
                while (sp > 0)
                {
                    int u = dfsNode[sp - 1];
                    int ch = dfsEdge[sp - 1];
                    if (ch >= 0)
                    {
                        dfsEdge[sp - 1] = domNext[ch];
                        domIn[rpoOrder[ch]] = timer++;
                        dfsNode[sp] = ch;
                        dfsEdge[sp] = domFirst[ch];
                        ++sp;
                    }
                    else
                    {
                        domOut[rpoOrder[u]] = timer;
                        --sp;
                    }
                }

                // 6. Post-dominators via CHK on reversed CFG with virtual EXIT node (index blockCount)
                var ipdom = ComputePostDominators(n, rpoOrder, rpoIndex, succStart, succList, predStart, predList);

                // 7. Natural loops from dominator back-edges (u -> h where h dominates u)
                var isLoopHeader = new bool[blockCount];
                var loopHeader = new int[blockCount];
                var loopParent = new int[blockCount];
                var loopExit = new int[blockCount];
                for (int b = 0; b < blockCount; ++b)
                    loopHeader[b] = loopParent[b] = loopExit[b] = -1;

                // Collect back-edges per header; process headers in reverse RPO (innermost first)
                var cfg = new RegionCfg
                {
                    Entry = entry, Lo = lo, Hi = hi, Count = n,
                    RpoOrder = rpoOrder, RpoIndex = rpoIndex,
                    Idom = idom, DomIn = domIn, DomOut = domOut, Ipdom = ipdom,
                    IsLoopHeader = isLoopHeader, LoopHeader = loopHeader,
                    LoopParent = loopParent, LoopExit = loopExit,
                    SuccStart = succStart, SuccList = succList,
                    PredStart = predStart, PredList = predList, PredCount = predCount,
                };

                var loopStack = new int[blockCount];
                var inCurLoop = new int[blockCount]; // stamp array to avoid reallocating bool[]
                int stamp = 0;
                for (int ri = n - 1; ri >= 0; --ri)
                {
                    int h = rpoOrder[ri];
                    // Check if h has any back-edge u -> h (where h dominates u)
                    int ltop = 0;
                    ++stamp;
                    for (int e = predStart[h]; e < predStart[h + 1]; ++e)
                    {
                        int u = predList[e];
                        if (cfg.Dominates(h, u))
                        {
                            isLoopHeader[h] = true;
                            if (inCurLoop[u] != stamp)
                            {
                                inCurLoop[u] = stamp;
                                if (u != h) loopStack[ltop++] = u;
                            }
                        }
                    }
                    if (!isLoopHeader[h]) continue;

                    inCurLoop[h] = stamp;
                    // Reverse walk from back-edge sources to h
                    while (ltop > 0)
                    {
                        int cur = loopStack[--ltop];
                        for (int e = predStart[cur]; e < predStart[cur + 1]; ++e)
                        {
                            int p = predList[e];
                            if (inCurLoop[p] != stamp)
                            {
                                inCurLoop[p] = stamp;
                                if (p != h) loopStack[ltop++] = p;
                            }
                        }
                    }

                    // Link innermost loop membership and loop nesting
                    for (int i = ri; i < n; ++i)
                    {
                        int b = rpoOrder[i];
                        if (inCurLoop[b] != stamp) continue;
                        if (loopHeader[b] < 0)
                        {
                            loopHeader[b] = h;
                        }
                        else
                        {
                            // Walk up existing loop parent chain to attach outermost inner loop under h
                            int inner = loopHeader[b];
                            while (loopParent[inner] >= 0 && loopParent[inner] != h)
                                inner = loopParent[inner];
                            if (inner != h && loopParent[inner] < 0)
                                loopParent[inner] = h;
                        }
                    }

                    // Find primary exit block: prefer ipdom[h] if outside loop, else lowest-RPO exit target
                    int bestExit = -1;
                    int ip = ipdom[h];
                    if (ip >= 0 && inCurLoop[ip] != stamp)
                    {
                        bestExit = ip;
                    }
                    else
                    {
                        int bestRpo = int.MaxValue;
                        for (int i = ri; i < n; ++i)
                        {
                            int b = rpoOrder[i];
                            if (inCurLoop[b] != stamp) continue;
                            for (int e = succStart[b]; e < succStart[b + 1]; ++e)
                            {
                                int s = succList[e];
                                if (rpoIndex[s] >= 0 && inCurLoop[s] != stamp && rpoIndex[s] < bestRpo)
                                {
                                    bestRpo = rpoIndex[s];
                                    bestExit = s;
                                }
                            }
                        }
                    }
                    loopExit[h] = bestExit;
                }

                // Pre-allocate labels for irreducible cycle targets (a non-dominator back-edge p -> b
                // where RpoIndex[p] >= RpoIndex[b]) so MarkLabel is emitted when b is visited first.
                for (int i = 0; i < n; ++i)
                {
                    int b = rpoOrder[i];
                    for (int e = predStart[b]; e < predStart[b + 1]; ++e)
                    {
                        int p = predList[e];
                        if (rpoIndex[p] >= i && !cfg.Dominates(b, p))
                        {
                            GetLabel(b);
                            break;
                        }
                    }
                }

                return cfg;
            }

            static void AddUniqueSucc(List<int> buf, int start, int target)
            {
                for (int k = start; k < buf.Count; ++k)
                    if (buf[k] == target) return;
                buf.Add(target);
            }

            static int IntersectDom(int[] idom, int a, int b)
            {
                while (a != b)
                {
                    while (a > b) a = idom[a];
                    while (b > a) b = idom[b];
                }
                return a;
            }

            int[] ComputePostDominators(
                int n, int[] rpoOrder, int[] rpoIndex,
                int[] succStart, int[] succList,
                int[] predStart, int[] predList)
            {
                var ipdom = new int[blockCount];
                for (int b = 0; b < blockCount; ++b) ipdom[b] = -1;

                // Reverse DFS from virtual EXIT (all blocks with 0 region-local successors)
                int exitId = blockCount;
                var revVis = new bool[blockCount + 1];
                var revPost = new int[n + 1];
                int revPostCount = 0;

                var exitSuccs = new List<int>();
                for (int i = 0; i < n; ++i)
                {
                    int b = rpoOrder[i];
                    if (succStart[b] == succStart[b + 1])
                        exitSuccs.Add(b);
                }
                if (exitSuccs.Count == 0)
                    return ipdom; // infinite loop with no exit

                var dfsNode = new int[n + 1];
                var dfsEdge = new int[n + 1];
                int sp = 0;
                revVis[exitId] = true;
                dfsNode[0] = exitId;
                dfsEdge[0] = 0;
                ++sp;
                while (sp > 0)
                {
                    int u = dfsNode[sp - 1];
                    int e = dfsEdge[sp - 1];
                    if (u == exitId)
                    {
                        if (e < exitSuccs.Count)
                        {
                            dfsEdge[sp - 1] = e + 1;
                            int v = exitSuccs[e];
                            if (!revVis[v])
                            {
                                revVis[v] = true;
                                dfsNode[sp] = v;
                                dfsEdge[sp] = predStart[v];
                                ++sp;
                            }
                        }
                        else
                        {
                            revPost[revPostCount++] = u;
                            --sp;
                        }
                    }
                    else
                    {
                        if (e < predStart[u + 1])
                        {
                            dfsEdge[sp - 1] = e + 1;
                            int v = predList[e];
                            if (!revVis[v])
                            {
                                revVis[v] = true;
                                dfsNode[sp] = v;
                                dfsEdge[sp] = predStart[v];
                                ++sp;
                            }
                        }
                        else
                        {
                            revPost[revPostCount++] = u;
                            --sp;
                        }
                    }
                }

                int rn = revPostCount;
                var revRpoOrder = new int[rn];
                var revRpoIndex = new int[blockCount + 1];
                for (int b = 0; b <= blockCount; ++b) revRpoIndex[b] = -1;
                for (int i = 0; i < rn; ++i)
                {
                    int b = revPost[rn - 1 - i];
                    revRpoOrder[i] = b;
                    revRpoIndex[b] = i;
                }

                var pdom = new int[rn];
                for (int i = 0; i < rn; ++i) pdom[i] = -1;
                pdom[0] = 0; // EXIT
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 1; i < rn; ++i)
                    {
                        int b = revRpoOrder[i];
                        int newPdom = -1;
                        if (succStart[b] == succStart[b + 1])
                        {
                            newPdom = 0; // EXIT
                        }
                        else
                        {
                            for (int e = succStart[b]; e < succStart[b + 1]; ++e)
                            {
                                int s = succList[e];
                                int sr = rpoIndex[s] >= 0 ? revRpoIndex[s] : -1;
                                if (sr >= 0 && pdom[sr] >= 0)
                                    newPdom = newPdom < 0 ? sr : IntersectDom(pdom, sr, newPdom);
                            }
                        }
                        if (newPdom >= 0 && pdom[i] != newPdom)
                        {
                            pdom[i] = newPdom;
                            changed = true;
                        }
                    }
                }

                for (int i = 1; i < rn; ++i)
                {
                    int b = revRpoOrder[i];
                    int pd = pdom[i];
                    if (pd > 0)
                        ipdom[b] = revRpoOrder[pd];
                }
                return ipdom;
            }
            #endregion

            #region Structured Region Emission
            /// <summary>
            /// Emits a sequence of blocks starting at <paramref name="b"/> inside <paramref name="region"/>.
            /// Returns the block where execution stopped without terminating, or -1 if this path terminated.
            /// </summary>
            int EmitSequence(int b, ref Frame f, ActiveRegion region, bool isEntry = false)
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
                var cfg = region.Cfg;
                for (; ; isEntry = false)
                {
                    if (b < 0) return -1;

                    // 1. Exiting the current try/catch/finally/filter region?
                    if (b < cfg.Lo || b >= cfg.Hi)
                        return HandleRegionExit(b, ref f, region);

                    // 2. Hit an enclosing loop's header (continue) or exit (break)?
                    if (!isEntry)
                    {
                        if (region.LoopHeader >= 0 && b == region.LoopHeader)
                        {
                            StoreEntryTemps(b, ref f);
                            region.ContinueUsed = true;
                            Emit(ref f, exp.Continue(region.ContinueLabel));
                            return -1;
                        }
                        if (region.LoopHeader >= 0 && !cfg.InLoop(b, region.LoopHeader))
                            return HandleLoopExit(b, ref f, region);
                    }

                    // 3. Reached the caller's stop boundary?
                    if (b == region.Stop)
                        return b;

                    // 4. Ownership / scoping check: can this block be emitted inline in this region?
                    if (!isEntry && !CanEmitInline(b, region))
                        return b;

                    // 5. Already emitted earlier in this region (e.g. irreducible backward jump)?
                    if (blockEmitted[b])
                    {
                        StoreEntryTemps(b, ref f);
                        Emit(ref f, exp.Goto(GetLabel(b)));
                        RegisterPending(b, region);
                        return -1;
                    }

                    // 5b. If an un-emitted pending sibling in this region has lower RPO than b
                    // (e.g. a switch case or branch tail that will jump to b), defer b so it is
                    // emitted after its earlier-RPO predecessors with its label defined.
                    if (!isEntry && HasEarlierPending(b, region))
                    {
                        StoreEntryTemps(b, ref f);
                        Emit(ref f, exp.Goto(GetLabel(b)));
                        RegisterPending(b, region);
                        return -1;
                    }

                    // 6. Entering a natural loop whose header is b?
                    if (cfg.IsLoopHeader[b] && !(isEntry && b == region.LoopHeader))
                    {
                        b = EmitLoop(b, ref f, region);
                        continue;
                    }

                    // 7. Entering a child TryNode starting at b?
                    var childTry = FindChildTry(region.Try, b, cfg.Lo, cfg.Hi);
                    if (childTry != null)
                    {
                        if (hasLabel[b] && !labelEmitted[b])
                        {
                            labelEmitted[b] = true;
                            Emit(ref f, exp.MarkLabel(blockLabels[b]));
                        }
                        b = EmitTry(childTry, ref f, region);
                        continue;
                    }

                    // 8. Emit this basic block's instructions
                    blockEmitted[b] = true;
                    if (hasLabel[b] && !labelEmitted[b])
                    {
                        labelEmitted[b] = true;
                        Emit(ref f, exp.MarkLabel(blockLabels[b]));
                    }

                    int start = blockStart[b];
                    int end = blockStart[b + 1];
                    int nextBlock = -1;
                    bool terminated = false;
                    for (int i = start; i < end; ++i)
                    {
                        ref var x = ref code[i];
                        var op = x.OpCode.Type();
                        if (op == OpType.Br || op == OpType.Br_s)
                        {
                            nextBlock = BlockIndex(x.Operand.Label);
                            break;
                        }
                        if (op == OpType.Leave || op == OpType.Leave_s)
                        {
                            Discard(ref f);
                            nextBlock = BlockIndex(x.Operand.Label);
                            break;
                        }
                        if (x.OpCode.FlowControl == FlowControl.Cond_Branch)
                        {
                            if (op == OpType.Switch)
                                nextBlock = EmitSwitch(b, i, ref f, region, out terminated);
                            else
                                nextBlock = EmitBranch(b, i, ref f, region, out terminated);
                            break;
                        }
                        if (StepInstruction(i, ref f, region.Kind, out terminated))
                        {
                            if (terminated) break;
                        }
                    }

                    if (terminated)
                        return -1;

                    if (nextBlock < 0)
                        nextBlock = b + 1 < blockCount ? b + 1 : -1;

                    b = nextBlock;
                }
            }

            /// <summary>
            /// Whether block <paramref name="b"/> is exclusively owned by <paramref name="region"/>
            /// (so emitting it inline never places a label out of scope of an external jump).
            /// </summary>
            static bool CanEmitInline(int b, ActiveRegion region)
            {
                var cfg = region.Cfg;
                if (!cfg.Dominates(region.Entry, b))
                    return false;
                if (region.LoopHeader >= 0 && !cfg.InLoop(b, region.LoopHeader))
                    return false;
                // Every non-back-edge predecessor of b must also be dominated by region.Entry
                for (int e = cfg.PredStart[b]; e < cfg.PredStart[b + 1]; ++e)
                {
                    int p = cfg.PredList[e];
                    if (!cfg.Dominates(region.Entry, p))
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Whether <paramref name="target"/> can be emitted inline as the entry of a child arm
            /// forked directly from <paramref name="branchBlock"/> (including any fused short-circuit blocks).
            /// </summary>
            bool IsArmOwned(int branchBlock, int target, ActiveRegion region)
            {
                var cfg = region.Cfg;
                if (target < cfg.Lo || target >= cfg.Hi || target == region.Stop || blockEmitted[target])
                    return false;
                if (region.LoopHeader >= 0 && (target == region.LoopHeader || !cfg.InLoop(target, region.LoopHeader)))
                    return false;
                // target is owned by this arm iff every predecessor of target is either branchBlock,
                // a short-circuit block fused into branchBlock, or dominated by target (loop back-edge).
                for (int e = cfg.PredStart[target]; e < cfg.PredStart[target + 1]; ++e)
                {
                    int p = cfg.PredList[e];
                    if (p != branchBlock && !(blockEmitted[p] && cfg.Dominates(branchBlock, p)) && !cfg.Dominates(target, p))
                        return false;
                }
                return true;
            }

            int EmitLoop(int header, ref Frame f, ActiveRegion parent)
            {
                var cfg = parent.Cfg;
                int exit = cfg.LoopExit[header];
                int depth = f.Depth;
                if (depth > 0)
                {
                    Spill(ref f);
                    StoreEntryTemps(header, ref f);
                }

                var breakLabel = exp.Label("break_" + header);
                var continueLabel = exp.Label("continue_" + header);
                var loopRegion = new ActiveRegion
                {
                    Parent = parent,
                    Cfg = cfg,
                    Kind = parent.Kind,
                    Try = parent.Try,
                    Entry = header,
                    Stop = -1,
                    LoopHeader = header,
                    LoopExit = exit,
                    BreakLabel = breakLabel,
                    ContinueLabel = continueLabel,
                };

                var bodyFrame = NewFrame(f.StackTop, f.StmtTop);
                if (depth > 0)
                    LoadEntryTemps(header, ref bodyFrame);

                int stopped = EmitSequence(header, ref bodyFrame, loopRegion, isEntry: true);
                if (stopped >= 0)
                {
                    // Fell out to a target without explicit break/continue
                    if (stopped == header)
                    {
                        StoreEntryTemps(header, ref bodyFrame);
                        loopRegion.ContinueUsed = true;
                        Emit(ref bodyFrame, exp.Continue(continueLabel));
                    }
                    else
                    {
                        HandleLoopExit(stopped, ref bodyFrame, loopRegion);
                    }
                }
                EmitPendingSiblings(ref bodyFrame, loopRegion, terminated: true);

                var bodyExpr = MaterializeBlock(bodyFrame.StmtBase, bodyFrame.StmtCount);
                Emit(ref f, exp.Loop(bodyExpr, loopRegion.BreakUsed ? breakLabel : default(T), loopRegion.ContinueUsed ? continueLabel : default(T)));

                if (exit >= 0 && entryTemps[exit] != null)
                    LoadEntryTemps(exit, ref f);

                return exit;
            }

            int HandleLoopExit(int target, ref Frame f, ActiveRegion region)
            {
                // Follow trivial `nop*; br dest` blocks (commonly emitted for `break;` / `continue;` in Debug IL)
                target = FollowTrivialBranches(target, region.Cfg);
                // Walk up enclosing loops to emit Break/Continue directly when targeting an enclosing loop
                for (var r = region; r != null && r.Cfg == region.Cfg; r = r.Parent)
                {
                    if (r.LoopHeader < 0) break;
                    if (target == r.LoopHeader)
                    {
                        StoreEntryTemps(target, ref f);
                        r.ContinueUsed = true;
                        Emit(ref f, exp.Continue(r.ContinueLabel));
                        return -1;
                    }
                    if (target == r.LoopExit)
                    {
                        StoreEntryTemps(target, ref f);
                        r.BreakUsed = true;
                        Emit(ref f, exp.Break(r.BreakLabel));
                        return -1;
                    }
                }
                // Arbitrary exit to a label outside the loop (e.g. goto done)
                StoreEntryTemps(target, ref f);
                Emit(ref f, exp.Goto(GetLabel(target)));
                RegisterPending(target, region);
                return -1;
            }

            int FollowTrivialBranches(int b, RegionCfg cfg)
            {
                for (int hops = 0; hops < 8 && b >= cfg.Lo && b < cfg.Hi; ++hops)
                {
                    int start = blockStart[b];
                    int end = blockStart[b + 1];
                    int i = start;
                    while (i < end - 1 && code[i].OpCode.Type() == OpType.Nop)
                        ++i;
                    if (i != end - 1)
                        break;
                    var op = code[i].OpCode.Type();
                    if (op == OpType.Br || op == OpType.Br_s)
                        b = BlockIndex(code[i].Operand.Label);
                    else
                        break;
                }
                return b;
            }

            bool HasEarlierPending(int b, ActiveRegion region)
            {
                if (region.Pending == null) return false;
                int bRpo = region.Cfg.RpoIndex[b];
                if (bRpo < 0) return false;
                for (int k = 0; k < region.Pending.Count; ++k)
                {
                    int pb = region.Pending[k];
                    if (!blockEmitted[pb] && region.Cfg.RpoIndex[pb] >= 0 && region.Cfg.RpoIndex[pb] < bRpo)
                        return true;
                }
                return false;
            }

            int HandleRegionExit(int target, ref Frame f, ActiveRegion region)
            {
                if (region.Kind == RegionKind.Finally || region.Kind == RegionKind.Fault || region.Kind == RegionKind.Filter)
                    throw new InvalidProgramException($"Control leaves a {region.Kind} block at {LabelOfBlock(target)}.");

                // Leaving a Try or Catch body via 'leave target'
                Discard(ref f);
                var tn = region.Try;
                if (tn != null && target == tn.NormalExit)
                {
                    // Normal fall-out of this try/catch into the outer region's continuation
                    return -1;
                }
                // Leaving to an outer loop break/continue or a non-default label!
                for (var r = region.Parent; r != null; r = r.Parent)
                {
                    if (r.LoopHeader >= 0)
                    {
                        if (target == r.LoopHeader)
                        {
                            r.ContinueUsed = true;
                            Emit(ref f, exp.Continue(r.ContinueLabel));
                            return -1;
                        }
                        if (target == r.LoopExit)
                        {
                            r.BreakUsed = true;
                            Emit(ref f, exp.Break(r.BreakLabel));
                            return -1;
                        }
                    }
                    if (r.Try != null && target == r.Try.NormalExit)
                        break;
                }
                Emit(ref f, exp.Goto(GetLabel(target)));
                RegisterPending(target, region.Parent);
                return -1;
            }

            int EmitTry(TryNode tn, ref Frame f, ActiveRegion parent)
            {
                if (f.Depth != 0)
                    throw new InvalidProgramException("The evaluation stack must be empty on entry to a protected region.");

                // 1. Protected try body
                var tryCfg = BuildRegionCfg(tn.TryStart, tn.TryStart, tn.TryEnd, tn);
                var tryRegion = new ActiveRegion
                {
                    Parent = parent, Cfg = tryCfg, Kind = RegionKind.Try, Try = tn,
                    Entry = tn.TryStart, Stop = -1, LoopHeader = -1, LoopExit = -1,
                };
                var tryFrame = NewFrame(f.StackTop, f.StmtTop);
                EmitSequence(tn.TryStart, ref tryFrame, tryRegion, isEntry: true);
                EmitPendingSiblings(ref tryFrame, tryRegion, terminated: true);
                var tryBody = MaterializeBlock(tryFrame.StmtBase, tryFrame.StmtCount);

                // 2. Handlers
                var first = tn.Clauses[0];
                if (first.IsFault)
                {
                    int hs = BlockIndex(first.HandlerStart);
                    int he = BlockIndex(first.HandlerEnd);
                    var faultBody = EmitHandlerRegion(hs, he, RegionKind.Fault, tn, parent, ref f, default(Slot), hasInitSlot: false);
                    Emit(ref f, exp.TryFault(tryBody, faultBody));
                }
                else if (first.IsFinally)
                {
                    int hs = BlockIndex(first.HandlerStart);
                    int he = BlockIndex(first.HandlerEnd);
                    var finBody = EmitHandlerRegion(hs, he, RegionKind.Finally, tn, parent, ref f, default(Slot), hasInitSlot: false);
                    Emit(ref f, exp.TryCatchFinally(tryBody, Array.Empty<CatchHandler<T>>(), finBody));
                }
                else
                {
                    var handlers = new CatchHandler<T>[tn.Clauses.Count];
                    for (int k = 0; k < tn.Clauses.Count; ++k)
                    {
                        var c = tn.Clauses[k];
                        int hs = BlockIndex(c.HandlerStart);
                        int he = BlockIndex(c.HandlerEnd);
                        var exType = c.IsCatch ? c.CatchType : typeof(object);
                        var exVar = exp.Param("_ex" + tempCount++, exType);
                        var exSlot = new Slot { Value = exVar, Type = exType, Flags = SlotFlags.Stable };
                        if (c.IsFilter)
                        {
                            int fs = BlockIndex(c.FilterStart);
                            int fe = fs < hs ? hs : he;
                            var filterExpr = EmitHandlerRegion(fs, fe, RegionKind.Filter, tn, parent, ref f, exSlot, hasInitSlot: true);
                            var handlerBody = EmitHandlerRegion(hs, he, RegionKind.Catch, tn, parent, ref f, exSlot, hasInitSlot: true);
                            handlers[k] = new CatchHandler<T>(exType, exVar, handlerBody, filterExpr);
                        }
                        else
                        {
                            var handlerBody = EmitHandlerRegion(hs, he, RegionKind.Catch, tn, parent, ref f, exSlot, hasInitSlot: true);
                            handlers[k] = new CatchHandler<T>(exType, exVar, handlerBody);
                        }
                    }
                    Emit(ref f, exp.TryCatchFinally(tryBody, handlers, default(T)));
                }

                return tn.NormalExit;
            }

            T EmitHandlerRegion(int hs, int he, RegionKind kind, TryNode tn, ActiveRegion parent, ref Frame caller, Slot initSlot, bool hasInitSlot)
            {
                var hCfg = BuildRegionCfg(hs, hs, he, tn);
                var hRegion = new ActiveRegion
                {
                    Parent = parent, Cfg = hCfg, Kind = kind, Try = tn,
                    Entry = hs, Stop = -1, LoopHeader = -1, LoopExit = -1,
                };
                var hFrame = NewFrame(caller.StackTop, caller.StmtTop);
                if (hasInitSlot)
                    Push(ref hFrame, initSlot);
                EmitSequence(hs, ref hFrame, hRegion, isEntry: true);
                EmitPendingSiblings(ref hFrame, hRegion, terminated: true);
                return MaterializeBlock(hFrame.StmtBase, hFrame.StmtCount);
            }

            int EmitBranch(int b, int instrIdx, ref Frame f, ActiveRegion region, out bool terminated)
            {
                terminated = false;
                ref var x = ref code[instrIdx];
                var cond = BranchTest(x, ref f, out var takenWhenTrue);
                int fallBlock = b + 1;
                int takenBlock = BlockIndex(x.Operand.Label);
                int trueTarget = takenWhenTrue ? takenBlock : fallBlock;
                int falseTarget = takenWhenTrue ? fallBlock : takenBlock;

                // Condition fusion for short-circuit && and || chains:
                // If one arm is a single-predecessor pure expression block ending in a conditional
                // branch that shares one of our targets, fuse into AndAlso / OrElse!
                FuseShortCircuit(b, ref cond, ref trueTarget, ref falseTarget, ref f, region);

                // Degenerate branch where both targets are identical
                if (trueTarget == falseTarget)
                    return trueTarget;

                Spill(ref f);
                var cfg = region.Cfg;
                int stop = cfg.Ipdom[b];
                if (stop < 0 || (region.LoopHeader >= 0 && !cfg.InLoop(stop, region.LoopHeader)))
                    stop = region.Stop;

                // Compute arm ownership before running either arm so blocks emitted by the first arm
                // are not mistaken for fused short-circuit predecessors of the second arm.
                bool trueOwned = IsArmOwned(b, trueTarget, region);
                bool falseOwned = IsArmOwned(b, falseTarget, region);

                // Fast guard shortcut for statement-level `if (cond) { return/throw/break/continue; } rest;`
                // Prevents deep recursion on long guard chains (e.g. 100 sequential `if (...) return;`).
                if (f.Depth == 0)
                {
                    bool trueIsGuard = trueOwned && cfg.Ipdom[trueTarget] < 0 && trueTarget != stop;
                    bool falseIsGuard = falseOwned && cfg.Ipdom[falseTarget] < 0 && falseTarget != stop;
                    if (trueIsGuard && !falseIsGuard)
                    {
                        var guardArm = RunArm(b, trueTarget, trueOwned, stop, ref f, region);
                        if (guardArm.ReachedBlock < 0)
                        {
                            if (guardArm.HasStmts)
                                Emit(ref f, exp.If(cond, guardArm.Stmts));
                            return falseTarget;
                        }
                        var otherArm = RunArm(b, falseTarget, falseOwned, stop, ref f, region);
                        return CombineTwoArms(b, cond, guardArm, otherArm, ref f, region, out terminated);
                    }
                    if (falseIsGuard && !trueIsGuard)
                    {
                        var guardArm = RunArm(b, falseTarget, falseOwned, stop, ref f, region);
                        if (guardArm.ReachedBlock < 0)
                        {
                            if (guardArm.HasStmts)
                                Emit(ref f, exp.If(exp.Not(cond), guardArm.Stmts));
                            return trueTarget;
                        }
                        var otherArm = RunArm(b, trueTarget, trueOwned, stop, ref f, region);
                        return CombineTwoArms(b, cond, otherArm, guardArm, ref f, region, out terminated);
                    }
                }

                var thenArm = RunArm(b, trueTarget, trueOwned, stop, ref f, region);
                var elseArm = RunArm(b, falseTarget, falseOwned, stop, ref f, region);
                return CombineTwoArms(b, cond, thenArm, elseArm, ref f, region, out terminated);
            }

            void FuseShortCircuit(int branchBlock, ref T cond, ref int trueTarget, ref int falseTarget, ref Frame f, ActiveRegion region)
            {
                for (; ; )
                {
                    if (TryPeekCondition(branchBlock, trueTarget, ref f, region, out var cond2, out var t2, out var f2))
                    {
                        if (f2 == falseTarget)
                        {
                            blockEmitted[trueTarget] = true;
                            cond = exp.AndAlso(cond, cond2);
                            branchBlock = trueTarget;
                            trueTarget = t2;
                            continue;
                        }
                        if (t2 == falseTarget)
                        {
                            blockEmitted[trueTarget] = true;
                            cond = exp.AndAlso(cond, exp.Not(cond2));
                            branchBlock = trueTarget;
                            trueTarget = f2;
                            continue;
                        }
                    }
                    if (TryPeekCondition(branchBlock, falseTarget, ref f, region, out cond2, out t2, out f2))
                    {
                        if (t2 == trueTarget)
                        {
                            blockEmitted[falseTarget] = true;
                            cond = exp.OrElse(cond, cond2);
                            branchBlock = falseTarget;
                            falseTarget = f2;
                            continue;
                        }
                        if (f2 == trueTarget)
                        {
                            blockEmitted[falseTarget] = true;
                            cond = exp.OrElse(cond, exp.Not(cond2));
                            branchBlock = falseTarget;
                            falseTarget = t2;
                            continue;
                        }
                    }
                    break;
                }
            }

            /// <summary>
            /// Checks if <paramref name="cand"/> is a single-predecessor, statement-free block ending
            /// in a conditional branch, and if so evaluates its condition using the arena above <paramref name="caller"/>.
            /// </summary>
            bool TryPeekCondition(int predBlock, int cand, ref Frame caller, ActiveRegion region, out T cond2, out int t2, out int f2)
            {
                cond2 = default(T);
                t2 = f2 = -1;
                var cfg = region.Cfg;
                if (cand < cfg.Lo || cand >= cfg.Hi || blockEmitted[cand] || hasLabel[cand])
                    return false;
                if (cfg.PredCount[cand] != 1 || cfg.PredList[cfg.PredStart[cand]] != predBlock)
                    return false;
                if (cfg.IsLoopHeader[cand] || FindChildTry(region.Try, cand, cfg.Lo, cfg.Hi) != null)
                    return false;

                int start = blockStart[cand];
                int end = blockStart[cand + 1];
                ref var last = ref code[end - 1];
                if (last.OpCode.FlowControl != FlowControl.Cond_Branch || last.OpCode.Type() == OpType.Switch)
                    return false;

                // Validate upfront that no instruction in [start, end-1) emits a statement
                for (int i = start; i < end - 1; ++i)
                {
                    ref var instr = ref code[i];
                    var op = instr.OpCode.Type();
                    if (MayEmitStatement(op))
                        return false;
                    if (op == OpType.Call || op == OpType.Callvirt)
                    {
                        var m = instr.ResolveMethod();
                        if (!(m is MethodInfo mi) || mi.ReturnType == typeof(void))
                            return false;
                    }
                }

                var pf = NewFrame(caller.StackTop, caller.StmtTop);
                for (int i = start; i < end - 1; ++i)
                {
                    try
                    {
                        StepInstruction(i, ref pf, region.Kind, out var term);
                        if (term || pf.StmtCount > 0)
                            return false;
                    }
                    catch (InvalidProgramException)
                    {
                        return false;
                    }
                }
                int needed = last.OpCode.Type() == OpType.Brtrue || last.OpCode.Type() == OpType.Brtrue_s ||
                             last.OpCode.Type() == OpType.Brfalse || last.OpCode.Type() == OpType.Brfalse_s ? 1 : 2;
                if (pf.Depth != needed)
                    return false;

                cond2 = BranchTest(last, ref pf, out var takenWhenTrue2);
                if (pf.StmtCount > 0)
                    return false;
                int fall2 = cand + 1;
                int taken2 = BlockIndex(last.Operand.Label);
                t2 = takenWhenTrue2 ? taken2 : fall2;
                f2 = takenWhenTrue2 ? fall2 : taken2;
                return true;
            }

            static bool MayEmitStatement(OpType op)
            {
                switch (op)
                {
                    case OpType.Stloc: case OpType.Stloc_s: case OpType.Stloc_0: case OpType.Stloc_1: case OpType.Stloc_2: case OpType.Stloc_3:
                    case OpType.Starg: case OpType.Starg_s:
                    case OpType.Stfld: case OpType.Stsfld:
                    case OpType.Stelem: case OpType.Stelem_i: case OpType.Stelem_i1: case OpType.Stelem_i2: case OpType.Stelem_i4:
                    case OpType.Stelem_i8: case OpType.Stelem_r4: case OpType.Stelem_r8: case OpType.Stelem_ref:
                    case OpType.Stobj: case OpType.Stind_i: case OpType.Stind_i1: case OpType.Stind_i2: case OpType.Stind_i4:
                    case OpType.Stind_i8: case OpType.Stind_r4: case OpType.Stind_r8: case OpType.Stind_ref:
                    case OpType.Cpblk: case OpType.Cpobj: case OpType.Initblk: case OpType.Initobj:
                    case OpType.Throw: case OpType.Rethrow: case OpType.Ret: case OpType.Jmp:
                    case OpType.Ckfinite: case OpType.Pop: case OpType.Dup:
                    case OpType.Calli: case OpType.Newobj:
                        return true;
                    default:
                        return false;
                }
            }

            ArmResult RunArm(int branchBlock, int target, bool owned, int stop, ref Frame parent, ActiveRegion parentRegion)
            {
                var cfg = parentRegion.Cfg;
                int depth = parent.Depth;

                // If target is already at stop, or not exclusively owned by this arm, don't execute it here!
                if (target == stop || target < cfg.Lo || target >= cfg.Hi)
                {
                    if (target < cfg.Lo || target >= cfg.Hi)
                    {
                        var exitFrame = ForkFrame(ref parent);
                        HandleRegionExit(target, ref exitFrame, parentRegion);
                        return FinishArm(-1, ref exitFrame, depth);
                    }
                    return new ArmResult { ReachedBlock = target, Low = depth, Depth = depth };
                }

                if (parentRegion.LoopHeader >= 0 && (target == parentRegion.LoopHeader || !cfg.InLoop(target, parentRegion.LoopHeader)))
                {
                    var exitFrame = ForkFrame(ref parent);
                    if (target == parentRegion.LoopHeader)
                    {
                        StoreEntryTemps(target, ref exitFrame);
                        parentRegion.ContinueUsed = true;
                        Emit(ref exitFrame, exp.Continue(parentRegion.ContinueLabel));
                    }
                    else
                    {
                        HandleLoopExit(target, ref exitFrame, parentRegion);
                    }
                    return FinishArm(-1, ref exitFrame, depth);
                }

                if (!owned || blockEmitted[target])
                    return new ArmResult { ReachedBlock = target, Low = depth, Depth = depth };

                var armFrame = ForkFrame(ref parent);
                var armRegion = new ActiveRegion
                {
                    Parent = parentRegion,
                    Cfg = cfg,
                    Kind = parentRegion.Kind,
                    Try = parentRegion.Try,
                    Entry = target,
                    Stop = stop,
                    LoopHeader = parentRegion.LoopHeader,
                    LoopExit = parentRegion.LoopExit,
                    BreakLabel = parentRegion.BreakLabel,
                    ContinueLabel = parentRegion.ContinueLabel,
                };

                int stopped = EmitSequence(target, ref armFrame, armRegion, isEntry: true);
                if (armRegion.BreakUsed) parentRegion.BreakUsed = true;
                if (armRegion.ContinueUsed) parentRegion.ContinueUsed = true;

                // Emit any pending sibling blocks owned by this arm before closing it
                if (armRegion.Pending != null)
                {
                    if (stopped >= 0)
                    {
                        // Guard against falling through into pending blocks
                        StoreEntryTemps(stopped, ref armFrame);
                        Emit(ref armFrame, exp.Goto(GetLabel(stopped)));
                        RegisterPending(stopped, parentRegion);
                        stopped = -1;
                    }
                    EmitPendingSiblings(ref armFrame, armRegion, terminated: true);
                }

                return FinishArm(stopped, ref armFrame, depth);
            }

            ArmResult FinishArm(int reached, ref Frame armFrame, int parentDepth)
            {
                var res = new ArmResult
                {
                    ReachedBlock = reached,
                    Low = armFrame.Low,
                    Depth = armFrame.Depth,
                    HasStmts = armFrame.StmtCount > 0,
                    Stmts = armFrame.StmtCount > 0 ? MaterializeBlock(armFrame.StmtBase, armFrame.StmtCount) : default(T),
                };
                if (reached >= 0)
                {
                    int produced = armFrame.Depth - armFrame.Low;
                    if (produced == 1)
                    {
                        res.SingleVal = stackBuf[armFrame.StackBase + armFrame.Low];
                    }
                    else if (produced > 1)
                    {
                        res.ExtraVals = new Slot[produced];
                        Array.Copy(stackBuf, armFrame.StackBase + armFrame.Low, res.ExtraVals, 0, produced);
                    }
                }
                return res;
            }

            int CombineTwoArms(int branchBlock, T cond, ArmResult thenArm, ArmResult elseArm, ref Frame f, ActiveRegion region, out bool terminated)
            {
                terminated = false;

                // Case 1: Both arms terminated (ret / throw / break / continue / goto)
                if (thenArm.ReachedBlock < 0 && elseArm.ReachedBlock < 0)
                {
                    var tBody = thenArm.HasStmts ? thenArm.Stmts : exp.Block(Array.Empty<T>());
                    var eBody = elseArm.HasStmts ? elseArm.Stmts : exp.Block(Array.Empty<T>());
                    Emit(ref f, exp.If(cond, tBody, eBody));
                    terminated = true;
                    return -1;
                }

                // Case 2: One arm terminated, the other reached a block M
                if (thenArm.ReachedBlock < 0 || elseArm.ReachedBlock < 0)
                {
                    bool thenTerm = thenArm.ReachedBlock < 0;
                    var termArm = thenTerm ? thenArm : elseArm;
                    var liveArm = thenTerm ? elseArm : thenArm;
                    int next = liveArm.ReachedBlock;
                    int produced = liveArm.Depth - liveArm.Low;

                    if (produced == 0)
                    {
                        // Statement-level if where one arm terminated (e.g. if (x < 0) throw ...;)
                        if (!liveArm.HasStmts)
                        {
                            if (termArm.HasStmts)
                                Emit(ref f, exp.If(thenTerm ? cond : exp.Not(cond), termArm.Stmts));
                        }
                        else
                        {
                            var tBody = thenArm.HasStmts ? thenArm.Stmts : exp.Block(Array.Empty<T>());
                            var eBody = elseArm.HasStmts ? elseArm.Stmts : exp.Block(Array.Empty<T>());
                            Emit(ref f, exp.If(cond, tBody, eBody));
                        }
                        Truncate(ref f, liveArm.Low);
                        return next;
                    }
                    else if (produced == 1)
                    {
                        // Value-producing expression where one arm throws/returns (e.g. a ? throw E : b)
                        Truncate(ref f, liveArm.Low);
                        var liveSlot = liveArm.SingleVal;
                        if (liveSlot.IsAddress)
                            throw new NotSupportedException("Conditionally selecting a managed address is not supported.");
                        var liveExpr = liveArm.HasStmts ? exp.Block(new[] { liveArm.Stmts, liveSlot.Value }) : liveSlot.Value;
                        var termExpr = termArm.HasStmts ? termArm.Stmts : exp.Block(Array.Empty<T>());
                        var result = thenTerm ? exp.If(cond, termExpr, liveExpr) : exp.If(cond, liveExpr, termExpr);
                        Push(ref f, new Slot { Value = result, Type = liveSlot.Type });
                        return next;
                    }
                    else
                    {
                        // Multiple values produced by live arm
                        Truncate(ref f, liveArm.Low);
                        var temps = new Slot[produced];
                        var liveList = new List<T>(produced + 1);
                        if (liveArm.HasStmts) liveList.Add(liveArm.Stmts);
                        for (int j = 0; j < produced; ++j)
                        {
                            var s = liveArm.ExtraVals[j];
                            var tv = Temp(s.Type);
                            temps[j] = new Slot { Value = tv, Type = s.Type, Flags = SlotFlags.Stable };
                            liveList.Add(exp.Assign(tv, s.Value));
                        }
                        var liveBlock = exp.Block(liveList);
                        var termExpr = termArm.HasStmts ? termArm.Stmts : exp.Block(Array.Empty<T>());
                        Emit(ref f, thenTerm ? exp.If(cond, termExpr, liveBlock) : exp.If(cond, liveBlock, termExpr));
                        for (int j = 0; j < produced; ++j) Push(ref f, temps[j]);
                        return next;
                    }
                }

                // Case 3: Both arms reached blocks — check if they reached the same merge block
                if (thenArm.ReachedBlock == elseArm.ReachedBlock)
                {
                    int merge = thenArm.ReachedBlock;
                    if (thenArm.Depth != elseArm.Depth)
                        throw new InvalidProgramException("Inconsistent evaluation stack depth at a merge point.");

                    int low = Math.Min(thenArm.Low, elseArm.Low);
                    int k = thenArm.Depth - low;
                    if (k == 0)
                    {
                        Truncate(ref f, low);
                        if (thenArm.HasStmts && elseArm.HasStmts)
                            Emit(ref f, exp.If(cond, thenArm.Stmts, elseArm.Stmts));
                        else if (thenArm.HasStmts)
                            Emit(ref f, exp.If(cond, thenArm.Stmts));
                        else if (elseArm.HasStmts)
                            Emit(ref f, exp.If(exp.Not(cond), elseArm.Stmts));
                        return merge;
                    }
                    if (k == 1)
                    {
                        var s1 = GetArmSlot(ref thenArm, ref f, low);
                        var s2 = GetArmSlot(ref elseArm, ref f, low);
                        if (s1.IsAddress || s2.IsAddress)
                            throw new NotSupportedException("Conditionally selecting a managed address is not supported.");
                        Truncate(ref f, low);
                        var type = UnifyTypes(s1, s2);
                        var v1 = Coerce(s1, type);
                        var v2 = Coerce(s2, type);
                        // Fold pure boolean ternary into AndAlso / OrElse / cond / Not(cond)
                        if (type == typeof(bool) && !thenArm.HasStmts && !elseArm.HasStmts)
                        {
                            if (IsConstBool(s2, false))
                            {
                                var folded = IsConstBool(s1, true) ? cond : exp.AndAlso(cond, v1.Value);
                                Push(ref f, new Slot { Value = folded, Type = typeof(bool) });
                                return merge;
                            }
                            if (IsConstBool(s1, true))
                            {
                                var folded = exp.OrElse(cond, v2.Value);
                                Push(ref f, new Slot { Value = folded, Type = typeof(bool) });
                                return merge;
                            }
                            if (IsConstBool(s1, false) && IsConstBool(s2, true))
                            {
                                Push(ref f, new Slot { Value = exp.Not(cond), Type = typeof(bool) });
                                return merge;
                            }
                        }
                        var e1 = thenArm.HasStmts ? exp.Block(new[] { thenArm.Stmts, v1.Value }) : v1.Value;
                        var e2 = elseArm.HasStmts ? exp.Block(new[] { elseArm.Stmts, v2.Value }) : v2.Value;
                        Push(ref f, new Slot { Value = exp.If(cond, e1, e2), Type = type });
                        return merge;
                    }
                    else
                    {
                        var temps = new Slot[k];
                        var tStmts = new List<T>(k + 1);
                        var eStmts = new List<T>(k + 1);
                        if (thenArm.HasStmts) tStmts.Add(thenArm.Stmts);
                        if (elseArm.HasStmts) eStmts.Add(elseArm.Stmts);
                        for (int j = 0; j < k; ++j)
                        {
                            var s1 = GetArmSlot(ref thenArm, ref f, low + j);
                            var s2 = GetArmSlot(ref elseArm, ref f, low + j);
                            if (s1.IsAddress || s2.IsAddress)
                                throw new NotSupportedException("Conditionally selecting a managed address is not supported.");
                            var type = UnifyTypes(s1, s2);
                            var tv = Temp(type);
                            temps[j] = new Slot { Value = tv, Type = type, Flags = SlotFlags.Stable };
                            tStmts.Add(exp.Assign(tv, Coerce(s1, type).Value));
                            eStmts.Add(exp.Assign(tv, Coerce(s2, type).Value));
                        }
                        Truncate(ref f, low);
                        Emit(ref f, exp.If(cond, exp.Block(tStmts), exp.Block(eStmts)));
                        for (int j = 0; j < k; ++j) Push(ref f, temps[j]);
                        return merge;
                    }
                }

                // Case 4: Both arms reached DIFFERENT blocks (e.g. unstructured jump / irreducible CFG).
                // Pick the lowest-RPO block owned by our region as fall-out M; the other arm emits Goto.
                var cfg = region.Cfg;
                int b1 = thenArm.ReachedBlock, b2 = elseArm.ReachedBlock;
                int chosen = PickContinuation(b1, b2, region);
                var thenExpr = ArmGotoIfNot(ref thenArm, chosen, ref f, region);
                var elseExpr = ArmGotoIfNot(ref elseArm, chosen, ref f, region);
                if (chosen < 0)
                {
                    Emit(ref f, exp.If(cond, thenExpr, elseExpr));
                    terminated = true;
                    return -1;
                }
                bool thenGoes = b1 == chosen;
                var chosenArm = thenGoes ? thenArm : elseArm;
                // Truncate stack to chosenArm's output (if chosenArm produced values, push them)
                if (chosenArm.Depth > 0 && entryTemps[chosen] != null)
                {
                    // If chosen already has entryTemps (because another path jumps to it), store into them
                    var tExpr = thenGoes ? ArmStoreAndFall(ref thenArm, chosen, ref f) : thenExpr;
                    var eExpr = !thenGoes ? ArmStoreAndFall(ref elseArm, chosen, ref f) : elseExpr;
                    Emit(ref f, exp.If(cond, tExpr, eExpr));
                    Truncate(ref f, 0);
                    LoadEntryTemps(chosen, ref f);
                    return chosen;
                }
                if (thenGoes && !thenArm.HasStmts)
                    Emit(ref f, exp.If(exp.Not(cond), elseExpr));
                else if (!thenGoes && !elseArm.HasStmts)
                    Emit(ref f, exp.If(cond, thenExpr));
                else
                    Emit(ref f, exp.If(cond, thenExpr, elseExpr));
                RestoreArmStack(ref chosenArm, ref f);
                return chosen;
            }

            int EmitSwitch(int b, int instrIdx, ref Frame f, ActiveRegion region, out bool terminated)
            {
                terminated = false;
                var val = SwitchValue(Pop(ref f));
                Spill(ref f);

                ref var x = ref code[instrIdx];
                var rawCases = new List<KeyValuePair<int, int>>();
                var uniqueStarts = new List<int>();
                foreach (var kv in x.ResolveBranches())
                {
                    int tb = BlockIndex(kv.Value);
                    rawCases.Add(new KeyValuePair<int, int>(kv.Key, tb));
                    if (!uniqueStarts.Contains(tb))
                        uniqueStarts.Add(tb);
                }
                int defaultBlock = b + 1;
                int defaultIdx = uniqueStarts.IndexOf(defaultBlock);
                if (defaultIdx < 0)
                {
                    defaultIdx = uniqueStarts.Count;
                    uniqueStarts.Add(defaultBlock);
                }

                var cfg = region.Cfg;
                int stop = cfg.Ipdom[b];
                if (stop < 0 || (region.LoopHeader >= 0 && !cfg.InLoop(stop, region.LoopHeader)))
                    stop = region.Stop;

                var owned = new bool[uniqueStarts.Count];
                for (int k = 0; k < uniqueStarts.Count; ++k)
                    owned[k] = IsArmOwned(b, uniqueStarts[k], region);

                var arms = new ArmResult[uniqueStarts.Count];
                for (int k = 0; k < uniqueStarts.Count; ++k)
                    arms[k] = RunArm(b, uniqueStarts[k], owned[k], stop, ref f, region);

                // Check if all non-terminated arms reached the same block
                int commonReached = -1;
                bool allTerminated = true;
                bool allSame = true;
                for (int k = 0; k < arms.Length; ++k)
                {
                    int r = arms[k].ReachedBlock;
                    if (r >= 0)
                    {
                        allTerminated = false;
                        if (commonReached < 0) commonReached = r;
                        else if (commonReached != r) allSame = false;
                    }
                }

                if (allTerminated)
                {
                    var bodies = new T[arms.Length];
                    for (int k = 0; k < arms.Length; ++k)
                        bodies[k] = arms[k].HasStmts ? arms[k].Stmts : exp.Block(Array.Empty<T>());
                    EmitSwitchExpr(val, rawCases, uniqueStarts, bodies, bodies[defaultIdx], ref f, pushResult: false, resultType: null);
                    terminated = true;
                    return -1;
                }

                if (allSame)
                {
                    int firstLive = -1;
                    for (int k = 0; k < arms.Length; ++k)
                        if (arms[k].ReachedBlock >= 0) { firstLive = k; break; }

                    int depth = arms[firstLive].Depth;
                    int low = arms[firstLive].Low;
                    for (int k = 0; k < arms.Length; ++k)
                    {
                        if (arms[k].ReachedBlock < 0) continue;
                        if (arms[k].Depth != depth)
                            throw new InvalidProgramException("Inconsistent evaluation stack depth at a switch merge point.");
                        if (arms[k].Low < low) low = arms[k].Low;
                    }
                    int produced = depth - low;
                    var bodies = new T[arms.Length];
                    if (produced == 0)
                    {
                        Truncate(ref f, low);
                        for (int k = 0; k < arms.Length; ++k)
                            bodies[k] = arms[k].HasStmts ? arms[k].Stmts : exp.Block(Array.Empty<T>());
                        EmitSwitchExpr(val, rawCases, uniqueStarts, bodies, bodies[defaultIdx], ref f, pushResult: false, resultType: null);
                    }
                    else if (produced == 1)
                    {
                        Type type = null;
                        for (int k = 0; k < arms.Length; ++k)
                        {
                            if (arms[k].ReachedBlock < 0) continue;
                            var s = GetArmSlot(ref arms[k], ref f, low);
                            if (s.IsAddress)
                                throw new NotSupportedException("Conditionally selecting a managed address is not supported.");
                            type = type == null ? s.Type : UnifyTypes(type, s);
                        }
                        for (int k = 0; k < arms.Length; ++k)
                        {
                            if (arms[k].ReachedBlock < 0)
                            {
                                bodies[k] = arms[k].HasStmts ? arms[k].Stmts : exp.Block(Array.Empty<T>());
                            }
                            else
                            {
                                var v = Coerce(GetArmSlot(ref arms[k], ref f, low), type).Value;
                                bodies[k] = arms[k].HasStmts ? exp.Block(new[] { arms[k].Stmts, v }) : v;
                            }
                        }
                        Truncate(ref f, low);
                        EmitSwitchExpr(val, rawCases, uniqueStarts, bodies, bodies[defaultIdx], ref f, pushResult: true, resultType: type);
                    }
                    else
                    {
                        var temps = new Slot[produced];
                        for (int j = 0; j < produced; ++j)
                        {
                            Type type = null;
                            for (int k = 0; k < arms.Length; ++k)
                            {
                                if (arms[k].ReachedBlock < 0) continue;
                                var s = GetArmSlot(ref arms[k], ref f, low + j);
                                if (s.IsAddress)
                                    throw new NotSupportedException("Conditionally selecting a managed address is not supported.");
                                type = type == null ? s.Type : UnifyTypes(type, s);
                            }
                            temps[j] = new Slot { Value = Temp(type), Type = type, Flags = SlotFlags.Stable };
                        }
                        for (int k = 0; k < arms.Length; ++k)
                        {
                            if (arms[k].ReachedBlock < 0)
                            {
                                bodies[k] = arms[k].HasStmts ? arms[k].Stmts : exp.Block(Array.Empty<T>());
                            }
                            else
                            {
                                var stmts = new List<T>(produced + 1);
                                if (arms[k].HasStmts) stmts.Add(arms[k].Stmts);
                                for (int j = 0; j < produced; ++j)
                                    stmts.Add(exp.Assign(temps[j].Value, Coerce(GetArmSlot(ref arms[k], ref f, low + j), temps[j].Type).Value));
                                bodies[k] = exp.Block(stmts);
                            }
                        }
                        Truncate(ref f, low);
                        EmitSwitchExpr(val, rawCases, uniqueStarts, bodies, bodies[defaultIdx], ref f, pushResult: false, resultType: null);
                        for (int j = 0; j < produced; ++j) Push(ref f, temps[j]);
                    }
                    return commonReached;
                }

                // Multi-target switch (e.g. goto case / fallthrough / break+continue):
                // pick the lowest-RPO owned continuation block as fall-out; other targets use Goto.
                int chosen = -1;
                for (int k = 0; k < arms.Length; ++k)
                    if (arms[k].ReachedBlock >= 0)
                        chosen = chosen < 0 ? arms[k].ReachedBlock : PickContinuation(chosen, arms[k].ReachedBlock, region);

                var multiBodies = new T[arms.Length];
                int chosenArmIdx = -1;
                for (int k = 0; k < arms.Length; ++k)
                {
                    if (arms[k].ReachedBlock == chosen && chosen >= 0)
                        chosenArmIdx = k;
                    multiBodies[k] = ArmGotoIfNot(ref arms[k], chosen, ref f, region);
                }
                EmitSwitchExpr(val, rawCases, uniqueStarts, multiBodies, multiBodies[defaultIdx], ref f, pushResult: false, resultType: null);
                if (chosen < 0)
                {
                    terminated = true;
                    return -1;
                }
                if (entryTemps[chosen] != null)
                {
                    Truncate(ref f, 0);
                    LoadEntryTemps(chosen, ref f);
                }
                else if (chosenArmIdx >= 0)
                {
                    RestoreArmStack(ref arms[chosenArmIdx], ref f);
                }
                return chosen;
            }

            void EmitSwitchExpr(T val, List<KeyValuePair<int, int>> rawCases, List<int> uniqueStarts, T[] bodies, T defaultBody, ref Frame f, bool pushResult, Type resultType)
            {
                var pairs = new KeyValuePair<int, T>[rawCases.Count];
                for (int k = 0; k < rawCases.Count; ++k)
                    pairs[k] = new KeyValuePair<int, T>(rawCases[k].Key, bodies[uniqueStarts.IndexOf(rawCases[k].Value)]);
                var sw = exp.Switch(val, pairs, defaultBody);
                if (pushResult)
                    Push(ref f, new Slot { Value = sw, Type = resultType });
                else
                    Emit(ref f, sw);
            }

            int PickContinuation(int b1, int b2, ActiveRegion region)
            {
                var cfg = region.Cfg;
                bool ok1 = b1 >= cfg.Lo && b1 < cfg.Hi && b1 != region.Stop && CanEmitInline(b1, region) && !blockEmitted[b1];
                bool ok2 = b2 >= cfg.Lo && b2 < cfg.Hi && b2 != region.Stop && CanEmitInline(b2, region) && !blockEmitted[b2];
                if (ok1 && ok2)
                    return cfg.RpoIndex[b1] <= cfg.RpoIndex[b2] ? b1 : b2;
                if (ok1) return b1;
                if (ok2) return b2;
                return -1;
            }

            T ArmGotoIfNot(ref ArmResult arm, int chosen, ref Frame parent, ActiveRegion region)
            {
                if (arm.ReachedBlock < 0 || arm.ReachedBlock == chosen)
                    return arm.HasStmts ? arm.Stmts : exp.Block(Array.Empty<T>());

                int target = arm.ReachedBlock;
                var stmts = new List<T>(4);
                if (arm.HasStmts) stmts.Add(arm.Stmts);
                if (arm.Depth > 0)
                    AppendArmEntryTempStores(ref arm, target, ref parent, stmts);
                stmts.Add(exp.Goto(GetLabel(target)));
                RegisterPending(target, region);
                return stmts.Count == 1 ? stmts[0] : exp.Block(stmts);
            }

            T ArmStoreAndFall(ref ArmResult arm, int target, ref Frame parent)
            {
                var stmts = new List<T>(4);
                if (arm.HasStmts) stmts.Add(arm.Stmts);
                if (arm.Depth > 0)
                    AppendArmEntryTempStores(ref arm, target, ref parent, stmts);
                return stmts.Count == 0 ? exp.Block(Array.Empty<T>()) : stmts.Count == 1 ? stmts[0] : exp.Block(stmts);
            }

            void AppendArmEntryTempStores(ref ArmResult arm, int target, ref Frame parent, List<T> stmts)
            {
                var temps = entryTemps[target];
                if (temps == null)
                {
                    temps = new Slot[arm.Depth];
                    for (int j = 0; j < arm.Depth; ++j)
                    {
                        var s = GetArmSlot(ref arm, ref parent, j);
                        temps[j] = new Slot { Value = Temp(s.Type), Type = s.Type, Flags = SlotFlags.Stable };
                    }
                    entryTemps[target] = temps;
                }
                for (int j = 0; j < arm.Depth; ++j)
                {
                    var s = GetArmSlot(ref arm, ref parent, j);
                    stmts.Add(exp.Assign(temps[j].Value, Coerce(s, temps[j].Type).Value));
                }
            }

            Slot GetArmSlot(ref ArmResult arm, ref Frame parent, int index)
            {
                if (index < arm.Low)
                    return stackBuf[parent.StackBase + index];
                int produced = arm.Depth - arm.Low;
                if (produced == 1)
                    return arm.SingleVal;
                return arm.ExtraVals[index - arm.Low];
            }

            void RestoreArmStack(ref ArmResult arm, ref Frame f)
            {
                int produced = arm.Depth - arm.Low;
                Truncate(ref f, arm.Low);
                if (produced == 1)
                    Push(ref f, arm.SingleVal);
                else if (produced > 1)
                    for (int j = 0; j < produced; ++j)
                        Push(ref f, arm.ExtraVals[j]);
            }

            T GetLabel(int b)
            {
                if (!hasLabel[b])
                {
                    hasLabel[b] = true;
                    blockLabels[b] = exp.Label("L_" + b);
                }
                return blockLabels[b];
            }

            void RegisterPending(int target, ActiveRegion from)
            {
                if (blockEmitted[target]) return;
                // Find the innermost enclosing region that dominates target (same CFG & loop)
                ActiveRegion owner = null;
                for (var r = from; r != null; r = r.Parent)
                {
                    if (target >= r.Cfg.Lo && target < r.Cfg.Hi && r.Cfg.Dominates(r.Entry, target)
                        && (r.LoopHeader < 0 || r.Cfg.InLoop(target, r.LoopHeader)))
                    {
                        owner = r;
                        break;
                    }
                }
                if (owner == null)
                {
                    // Walk to outermost region containing target
                    for (var r = from; r != null; r = r.Parent)
                        if (target >= r.Cfg.Lo && target < r.Cfg.Hi)
                            owner = r;
                }
                if (owner == null) return;
                if (owner.Pending == null)
                    owner.Pending = new List<int>();
                if (!owner.Pending.Contains(target))
                    owner.Pending.Add(target);
            }

            void EmitPendingSiblings(ref Frame f, ActiveRegion region, bool terminated)
            {
                if (region.Pending == null) return;
                var cfg = region.Cfg;
                for (; ; )
                {
                    // Pick the lowest-RPO un-emitted pending block for natural fallthrough ordering
                    int bestIdx = -1, bestRpo = int.MaxValue;
                    for (int k = 0; k < region.Pending.Count; ++k)
                    {
                        int pb = region.Pending[k];
                        if (blockEmitted[pb]) continue;
                        int rpo = cfg.RpoIndex[pb];
                        if (rpo >= 0 && rpo < bestRpo)
                        {
                            bestRpo = rpo;
                            bestIdx = k;
                        }
                    }
                    if (bestIdx < 0) break;

                    int target = region.Pending[bestIdx];
                    region.Pending.RemoveAt(bestIdx);
                    if (blockEmitted[target]) continue;

                    // If previous control flow didn't terminate, protect against accidental fallthrough
                    if (!terminated && region.Stop >= 0)
                    {
                        StoreEntryTemps(region.Stop, ref f);
                        Emit(ref f, exp.Goto(GetLabel(region.Stop)));
                        RegisterPending(region.Stop, region.Parent);
                    }

                    Truncate(ref f, 0);
                    if (entryTemps[target] != null)
                        LoadEntryTemps(target, ref f);

                    int stopped = EmitSequence(target, ref f, region, isEntry: true);
                    if (stopped >= 0)
                    {
                        if (stopped == region.Stop)
                        {
                            terminated = false;
                        }
                        else
                        {
                            StoreEntryTemps(stopped, ref f);
                            Emit(ref f, exp.Goto(GetLabel(stopped)));
                            RegisterPending(stopped, region);
                            terminated = true;
                        }
                    }
                    else
                    {
                        terminated = true;
                    }
                }
            }

            void StoreEntryTemps(int target, ref Frame f)
            {
                int depth = f.Depth;
                if (depth == 0) return;
                var temps = entryTemps[target];
                if (temps == null)
                {
                    temps = new Slot[depth];
                    for (int j = 0; j < depth; ++j)
                    {
                        var s = stackBuf[f.StackBase + j];
                        temps[j] = new Slot { Value = Temp(s.Type), Type = s.Type, Flags = SlotFlags.Stable };
                    }
                    entryTemps[target] = temps;
                }
                for (int j = 0; j < depth; ++j)
                {
                    var s = stackBuf[f.StackBase + j];
                    EmitRaw(ref f, exp.Assign(temps[j].Value, Coerce(s, temps[j].Type).Value));
                }
                Truncate(ref f, 0);
            }

            void LoadEntryTemps(int target, ref Frame f)
            {
                var temps = entryTemps[target];
                if (temps == null) return;
                for (int j = 0; j < temps.Length; ++j)
                    Push(ref f, temps[j]);
            }
            #endregion

            #region Instruction Execution
            /// <summary>
            /// Executes a single non-branch instruction. Returns true and sets <paramref name="terminated"/>
            /// if the instruction terminates control flow (ret/throw/rethrow/endfinally/endfilter/jmp).
            /// </summary>
            bool StepInstruction(int i, ref Frame f, RegionKind kind, out bool terminated)
            {
                terminated = false;
                ref var x = ref code[i];
                var op = x.OpCode.Type();

                switch (op)
                {
                    //--------------- arithmetic and logic --------------
                    case OpType.Add:
                    case OpType.Add_ovf:
                    case OpType.Add_ovf_un:
                    case OpType.Sub:
                    case OpType.Sub_ovf:
                    case OpType.Sub_ovf_un:
                    case OpType.Mul:
                    case OpType.Mul_ovf:
                    case OpType.Mul_ovf_un:
                    case OpType.Div:
                    case OpType.Div_un:
                    case OpType.Rem:
                    case OpType.Rem_un:
                    case OpType.And:
                    case OpType.Or:
                    case OpType.Xor:
                    {
                        var r = Pop(ref f);
                        var l = Pop(ref f);
                        Push(ref f, Arith(op, l, r));
                        break;
                    }
                    case OpType.Shl:
                    case OpType.Shr:
                    case OpType.Shr_un:
                    {
                        var r = Pop(ref f);
                        var l = Pop(ref f);
                        Push(ref f, Shift(op, l, r));
                        break;
                    }
                    case OpType.Neg:
                        Push(ref f, Negate(Pop(ref f)));
                        break;
                    case OpType.Not:
                    {
                        var v = Normalize(Pop(ref f));
                        Push(ref f, new Slot { Value = exp.Not(v.Value), Type = v.Type });
                        break;
                    }

                    //--------------- comparisons --------------
                    case OpType.Ceq:
                    {
                        var r = Pop(ref f);
                        var l = Pop(ref f);
                        Push(ref f, Compare(Cmp.Eq, l, r, false));
                        break;
                    }
                    case OpType.Cgt:
                    case OpType.Cgt_un:
                    {
                        var r = Pop(ref f);
                        var l = Pop(ref f);
                        Push(ref f, Compare(Cmp.Gt, l, r, op == OpType.Cgt_un));
                        break;
                    }
                    case OpType.Clt:
                    case OpType.Clt_un:
                    {
                        var r = Pop(ref f);
                        var l = Pop(ref f);
                        Push(ref f, Compare(Cmp.Lt, l, r, op == OpType.Clt_un));
                        break;
                    }
                    case OpType.Ckfinite:
                    {
                        var s = Pop(ref f);
                        if (!s.IsStable)
                        {
                            var t = Temp(s.Type);
                            Emit(ref f, exp.Assign(t, s.Value));
                            s = new Slot { Value = t, Type = s.Type, Flags = SlotFlags.Stable };
                        }
                        var single = s.Type == typeof(float);
                        var bad = exp.Or(
                            exp.Call(single ? r4IsNaN : r8IsNaN, false, default(T), new[] { s.Value }),
                            exp.Call(single ? r4IsInfinity : r8IsInfinity, false, default(T), new[] { s.Value }));
                        var error = exp.Throw(exp.New(arithError, new[] { exp.Constant("Value is not a finite number.") }));
                        Push(ref f, new Slot { Value = exp.If(bad, error, s.Value), Type = s.Type });
                        break;
                    }

                    //--------------- terminating instructions --------------
                    case OpType.Ret:
                    {
                        if (kind != RegionKind.Root)
                            throw new InvalidProgramException("ret is not allowed inside a protected region.");
                        if (returnType == typeof(void))
                        {
                            Discard(ref f);
                            EmitRaw(ref f, exp.Return());
                        }
                        else
                        {
                            var v = returnType.IsByRef ? PopAddress(ref f) : Coerce(Pop(ref f), returnType);
                            Discard(ref f);
                            EmitRaw(ref f, exp.Return(v.Value));
                        }
                        terminated = true;
                        return true;
                    }
                    case OpType.Throw:
                    {
                        var v = Pop(ref f);
                        Discard(ref f);
                        EmitRaw(ref f, exp.Throw(v.Value));
                        terminated = true;
                        return true;
                    }
                    case OpType.Rethrow:
                        Discard(ref f);
                        EmitRaw(ref f, exp.Rethrow());
                        terminated = true;
                        return true;
                    case OpType.Endfinally:
                        if (kind != RegionKind.Finally && kind != RegionKind.Fault)
                            throw new InvalidProgramException("endfinally outside of a finally or fault block.");
                        Discard(ref f);
                        terminated = true;
                        return true;
                    case OpType.Endfilter:
                    {
                        var v = Pop(ref f);
                        if (kind != RegionKind.Filter)
                            throw new InvalidProgramException("endfilter outside of a filter block.");
                        EmitRaw(ref f, ToBool(v).Value);
                        terminated = true;
                        return true;
                    }
                    case OpType.Jmp:
                        EmitRaw(ref f, exp.Jump(x.ResolveMethod()));
                        terminated = true;
                        return true;

                    //--------------- method calls --------------
#if DEBUG
                    case OpType.Ldftn:
                        Push(ref f, new Slot { Value = exp.GetPointer(x.ResolveMethod(), default(T)), Type = nativeInt });
                        break;
                    case OpType.Ldvirtftn:
                        Push(ref f, new Slot { Value = exp.GetPointer(x.ResolveMethod(), Pop(ref f).Value), Type = nativeInt });
                        break;
#endif
                    case OpType.Calli:
                        throw new NotSupportedException("Indirect calls are not supported.");
                    case OpType.Call:
                    case OpType.Callvirt:
                        DoCall(i, x, ref f);
                        break;
                    case OpType.Newobj:
                    {
                        var ctor = (ConstructorInfo)x.ResolveMethod();
                        var margs = PopArgs(ref f, ctor.GetParameters());
                        Push(ref f, new Slot { Value = exp.New(ctor, margs), Type = ctor.DeclaringType });
                        break;
                    }

                    //--------------- memory blocks --------------
                    case OpType.Cpblk:
                    {
                        var size = Pop(ref f);
                        var src = PopAny(ref f);
                        var dst = PopAny(ref f);
                        Emit(ref f, exp.BlockCopy(dst.Value, src.Value, size.Value));
                        break;
                    }
                    case OpType.Cpobj:
                    {
                        var src = PopAddress(ref f);
                        var dst = PopAddress(ref f);
                        Emit(ref f, exp.StructCopy(dst.Value, src.Value, x.ResolveType()));
                        break;
                    }
                    case OpType.Initblk:
                    {
                        var size = Pop(ref f);
                        var value = Pop(ref f);
                        var dst = PopAny(ref f);
                        Emit(ref f, exp.BlockInit(dst.Value, value.Value, size.Value));
                        break;
                    }
                    case OpType.Initobj:
                        Emit(ref f, exp.ObjectInit(PopAddress(ref f).Value, x.ResolveType()));
                        break;
                    case OpType.Sizeof:
                        Push(ref f, new Slot { Value = exp.SizeOf(x.ResolveType()), Type = typeof(int), Flags = SlotFlags.Stable });
                        break;
                    case OpType.Localloc:
                        Push(ref f, new Slot { Value = exp.LocalAlloc(Pop(ref f).Value), Type = nativeInt });
                        break;

                    //--------------- loads and stores --------------
                    case OpType.Ldarg:
                    case OpType.Ldarg_s:
                    case OpType.Ldarg_0:
                    case OpType.Ldarg_1:
                    case OpType.Ldarg_2:
                    case OpType.Ldarg_3:
                    {
                        int k = ArgIndex(x);
                        var flags = (!argWritten[k] ? SlotFlags.Stable : SlotFlags.None) |
                                    (argIsAddress[k] ? SlotFlags.Address : SlotFlags.None);
                        Push(ref f, new Slot { Value = args[k], Type = argTypes[k], Flags = flags });
                        break;
                    }
                    case OpType.Ldarga:
                    case OpType.Ldarga_s:
                    {
                        int k = ArgIndex(x);
                        Push(ref f, new Slot
                        {
                            Value = exp.AddressOf(args[k]),
                            Type = argTypes[k],
                            Flags = SlotFlags.Stable | SlotFlags.Address,
                        });
                        break;
                    }
                    case OpType.Starg:
                    case OpType.Starg_s:
                    {
                        int k = ArgIndex(x);
                        if (argIsAddress[k])
                            throw new NotSupportedException("Rebinding a ref parameter is not supported.");
                        Emit(ref f, exp.Assign(args[k], Coerce(Pop(ref f), argTypes[k]).Value));
                        break;
                    }
                    case OpType.Ldloc:
                    case OpType.Ldloc_s:
                    case OpType.Ldloc_0:
                    case OpType.Ldloc_1:
                    case OpType.Ldloc_2:
                    case OpType.Ldloc_3:
                    {
                        int k = LocalIndex(x);
                        var flags = (!localWritten[k] ? SlotFlags.Stable : SlotFlags.None) |
                                    (localIsAddress[k] ? SlotFlags.Address : SlotFlags.None);
                        Push(ref f, new Slot { Value = locals[k], Type = localTypes[k], Flags = flags });
                        break;
                    }
                    case OpType.Ldloca:
                    case OpType.Ldloca_s:
                    {
                        int k = LocalIndex(x);
                        Push(ref f, new Slot
                        {
                            Value = exp.AddressOf(locals[k]),
                            Type = localTypes[k],
                            Flags = SlotFlags.Stable | SlotFlags.Address,
                        });
                        break;
                    }
                    case OpType.Stloc:
                    case OpType.Stloc_s:
                    case OpType.Stloc_0:
                    case OpType.Stloc_1:
                    case OpType.Stloc_2:
                    case OpType.Stloc_3:
                    {
                        int k = LocalIndex(x);
                        if (localIsAddress[k])
                            throw new NotSupportedException("Ref locals are not supported.");
                        Emit(ref f, exp.Assign(locals[k], Coerce(Pop(ref f), localTypes[k]).Value));
                        break;
                    }
                    case OpType.Ldfld:
                    {
                        var fld = x.ResolveField();
                        var obj = PopAny(ref f);
                        Push(ref f, new Slot { Value = exp.Field(fld, fld.IsStatic ? default(T) : obj.Value), Type = fld.FieldType });
                        break;
                    }
                    case OpType.Ldsfld:
                    {
                        var fld = x.ResolveField();
                        Push(ref f, new Slot { Value = exp.Field(fld, default(T)), Type = fld.FieldType });
                        break;
                    }
                    case OpType.Ldflda:
                    {
                        var fld = x.ResolveField();
                        var obj = PopAny(ref f);
                        if (fld.IsStatic)
                        {
                            Push(ref f, new Slot { Value = exp.AddressOf(exp.Field(fld, default(T))), Type = fld.FieldType, Flags = SlotFlags.Stable | SlotFlags.Address });
                        }
                        else if (obj.IsAddress && !obj.IsStable && (obj.IsElement || obj.IsFieldAddr))
                        {
                            // Stabilize chained address (e.g. a[f()].X += 1) before taking field address
                            Spill(ref f);
                            obj = Stabilize(ref f, obj);
                            Push(ref f, new Slot { Value = exp.AddressOf(exp.Field(fld, obj.Value)), Type = fld.FieldType, Flags = SlotFlags.Stable | SlotFlags.Address });
                        }
                        else if (obj.IsStable || obj.IsAddress)
                        {
                            var flags = SlotFlags.Address | (obj.IsStable ? SlotFlags.Stable : SlotFlags.None);
                            Push(ref f, new Slot { Value = exp.AddressOf(exp.Field(fld, obj.Value)), Type = fld.FieldType, Flags = flags });
                        }
                        else
                        {
                            Push(ref f, new Slot
                            {
                                Value = exp.AddressOf(exp.Field(fld, obj.Value)),
                                Type = fld.FieldType,
                                Flags = SlotFlags.Address | SlotFlags.FieldAddr,
                                AddrBase = obj.Value,
                                AddrBaseType = obj.Type,
                                AddrBaseStable = obj.IsStable,
                                AddrField = fld,
                            });
                        }
                        break;
                    }
                    case OpType.Ldsflda:
                    {
                        var fld = x.ResolveField();
                        Push(ref f, new Slot { Value = exp.AddressOf(exp.Field(fld, default(T))), Type = fld.FieldType, Flags = SlotFlags.Stable | SlotFlags.Address });
                        break;
                    }
                    case OpType.Stfld:
                    {
                        var fld = x.ResolveField();
                        var v = Pop(ref f);
                        var obj = PopAny(ref f);
                        Emit(ref f, exp.Assign(exp.Field(fld, fld.IsStatic ? default(T) : obj.Value), Coerce(v, fld.FieldType).Value));
                        break;
                    }
                    case OpType.Stsfld:
                    {
                        var fld = x.ResolveField();
                        Emit(ref f, exp.Assign(exp.Field(fld, default(T)), Coerce(Pop(ref f), fld.FieldType).Value));
                        break;
                    }
                    case OpType.Ldelema:
                    {
                        var idxSlot = Pop(ref f);
                        var arrSlot = Pop(ref f);
                        var i32 = Index32(idxSlot);
                        var elemType = x.ResolveType();
                        var flags = SlotFlags.Address | SlotFlags.Element | (arrSlot.IsStable && idxSlot.IsStable ? SlotFlags.Stable : SlotFlags.None);
                        Push(ref f, new Slot
                        {
                            Value = exp.AddressOf(exp.ArrayGet(arrSlot.Value, i32.Value)),
                            Type = elemType,
                            Flags = flags,
                            AddrBase = arrSlot.Value,
                            AddrBaseType = arrSlot.Type,
                            AddrBaseStable = arrSlot.IsStable,
                            AddrIndex = i32.Value,
                            AddrIndexType = typeof(int),
                            AddrIndexStable = idxSlot.IsStable,
                        });
                        break;
                    }
                    case OpType.Ldelem:
                    case OpType.Ldelem_ref:
                    case OpType.Ldelem_i:
                    case OpType.Ldelem_i1:
                    case OpType.Ldelem_i2:
                    case OpType.Ldelem_i4:
                    case OpType.Ldelem_i8:
                    case OpType.Ldelem_r4:
                    case OpType.Ldelem_r8:
                    case OpType.Ldelem_u1:
                    case OpType.Ldelem_u2:
                    case OpType.Ldelem_u4:
                    {
                        var idxSlot = Pop(ref f);
                        var arrSlot = Pop(ref f);
                        var elemType = op == OpType.Ldelem
                            ? x.ResolveType()
                            : (arrSlot.Type.IsArray ? arrSlot.Type.GetElementType() : ElemTypeFor(op));
                        Push(ref f, new Slot { Value = exp.ArrayGet(arrSlot.Value, Index32(idxSlot).Value), Type = elemType });
                        break;
                    }
                    case OpType.Stelem:
                    case OpType.Stelem_i:
                    case OpType.Stelem_i1:
                    case OpType.Stelem_i2:
                    case OpType.Stelem_i4:
                    case OpType.Stelem_i8:
                    case OpType.Stelem_r4:
                    case OpType.Stelem_r8:
                    case OpType.Stelem_ref:
                    {
                        var v = Pop(ref f);
                        var idxSlot = Pop(ref f);
                        var arrSlot = Pop(ref f);
                        var elemType = arrSlot.Type.IsArray ? arrSlot.Type.GetElementType() : null;
                        Emit(ref f, exp.ArraySet(arrSlot.Value, Index32(idxSlot).Value, elemType == null ? v.Value : Coerce(v, elemType).Value));
                        break;
                    }
                    case OpType.Ldlen:
                        Push(ref f, new Slot { Value = exp.ArrayLength(Pop(ref f).Value), Type = typeof(int) });
                        break;
                    case OpType.Ldobj:
                    case OpType.Ldind_ref:
                    case OpType.Ldind_i:
                    case OpType.Ldind_i1:
                    case OpType.Ldind_i2:
                    case OpType.Ldind_i4:
                    case OpType.Ldind_i8:
                    case OpType.Ldind_r4:
                    case OpType.Ldind_r8:
                    case OpType.Ldind_u1:
                    case OpType.Ldind_u2:
                    case OpType.Ldind_u4:
                    {
                        var addr = PopAddress(ref f);
                        var valType = op == OpType.Ldobj ? x.ResolveType() : (addr.Type ?? IndTypeFor(op));
                        Push(ref f, new Slot { Value = exp.AddressGet(addr.Value), Type = valType });
                        break;
                    }
                    case OpType.Stobj:
                    {
                        var v = Pop(ref f);
                        var addr = PopAddress(ref f);
                        var dstType = x.ResolveType();
                        Emit(ref f, exp.Assign(addr.Value, Coerce(v, dstType).Value));
                        break;
                    }
                    case OpType.Stind_ref:
                    case OpType.Stind_i:
                    case OpType.Stind_i1:
                    case OpType.Stind_i2:
                    case OpType.Stind_i4:
                    case OpType.Stind_i8:
                    case OpType.Stind_r4:
                    case OpType.Stind_r8:
                    {
                        var v = Pop(ref f);
                        var addr = PopAddress(ref f);
                        var dstType = addr.Type != null && !addr.Type.IsPointer ? addr.Type : null;
                        Emit(ref f, exp.AddressSet(addr.Value, dstType == null ? v.Value : Coerce(v, dstType).Value));
                        break;
                    }
                    case OpType.Arglist:
                        Push(ref f, new Slot { Value = exp.ArgumentList(), Type = typeof(RuntimeArgumentHandle) });
                        break;

                    //--------------- constants --------------
                    case OpType.Ldc_i4_m1: PushInt(ref f, -1); break;
                    case OpType.Ldc_i4_0: PushInt(ref f, 0); break;
                    case OpType.Ldc_i4_1: PushInt(ref f, 1); break;
                    case OpType.Ldc_i4_2: PushInt(ref f, 2); break;
                    case OpType.Ldc_i4_3: PushInt(ref f, 3); break;
                    case OpType.Ldc_i4_4: PushInt(ref f, 4); break;
                    case OpType.Ldc_i4_5: PushInt(ref f, 5); break;
                    case OpType.Ldc_i4_6: PushInt(ref f, 6); break;
                    case OpType.Ldc_i4_7: PushInt(ref f, 7); break;
                    case OpType.Ldc_i4_8: PushInt(ref f, 8); break;
                    case OpType.Ldc_i4: PushInt(ref f, x.Operand.Int32); break;
                    case OpType.Ldc_i4_s: PushInt(ref f, (int)x.Operand.Int8); break;
                    case OpType.Ldc_i8:
                    {
                        var v = x.Operand.Int64;
                        Push(ref f, new Slot { Value = exp.Constant(v), Type = typeof(long), Const = ConstKind.Int64, ConstVal = new Operand(v), Flags = SlotFlags.Stable });
                        break;
                    }
                    case OpType.Ldc_r4:
                    {
                        var v = x.Operand.Float32;
                        Push(ref f, new Slot { Value = exp.Constant(v), Type = typeof(float), Const = ConstKind.Float32, ConstVal = new Operand(v), Flags = SlotFlags.Stable });
                        break;
                    }
                    case OpType.Ldc_r8:
                    {
                        var v = x.Operand.Float64;
                        Push(ref f, new Slot { Value = exp.Constant(v), Type = typeof(double), Const = ConstKind.Float64, ConstVal = new Operand(v), Flags = SlotFlags.Stable });
                        break;
                    }
                    case OpType.Ldnull:
                        Push(ref f, new Slot { Value = exp.Constant<object>(null), Type = typeof(object), Const = ConstKind.Null, Flags = SlotFlags.Stable });
                        break;
                    case OpType.Ldstr:
                    {
                        var s = x.ResolveString();
                        Push(ref f, new Slot { Value = exp.Constant(s), Type = typeof(string), Const = ConstKind.Ref, ConstObj = s, Flags = SlotFlags.Stable });
                        break;
                    }
                    case OpType.Ldtoken:
                    {
                        var member = x.ResolveMember();
                        if (member is Type type)
                            Push(ref f, new Slot { Value = exp.Constant(type.TypeHandle, typeof(RuntimeTypeHandle)), Type = typeof(RuntimeTypeHandle), Const = ConstKind.Ref, ConstObj = type.TypeHandle, Flags = SlotFlags.Stable });
                        else if (member is MethodBase method)
                            Push(ref f, new Slot { Value = exp.Constant(method.MethodHandle, typeof(RuntimeMethodHandle)), Type = typeof(RuntimeMethodHandle), Const = ConstKind.Ref, ConstObj = method.MethodHandle, Flags = SlotFlags.Stable });
                        else if (member is FieldInfo field)
                            Push(ref f, new Slot { Value = exp.Constant(field.FieldHandle, typeof(RuntimeFieldHandle)), Type = typeof(RuntimeFieldHandle), Const = ConstKind.Ref, ConstObj = field.FieldHandle, Flags = SlotFlags.Stable });
                        else
                            throw new NotSupportedException("Unsupported ldtoken operand: " + x);
                        break;
                    }

                    //--------------- stack manipulation --------------
                    case OpType.Dup:
                    {
                        var s = PopAny(ref f);
                        if (!s.IsStable)
                        {
                            Spill(ref f);
                            s = Stabilize(ref f, s);
                        }
                        var dup = exp.Duplicate(s.Value);
                        var copy = new Slot
                        {
                            Value = dup,
                            Type = s.Type,
                            Const = s.Const,
                            ConstVal = s.ConstVal,
                            ConstObj = s.ConstObj,
                            Flags = SlotFlags.Stable | (s.Flags & (SlotFlags.Address | SlotFlags.FromCmp)),
                        };
                        Push(ref f, copy);
                        Push(ref f, copy);
                        break;
                    }
                    case OpType.Pop:
                    {
                        var s = PopAny(ref f);
                        if (!s.IsStable)
                            Emit(ref f, s.Value);
                        break;
                    }

                    //--------------- types and conversions --------------
                    case OpType.Box:
                    {
                        var boxType = x.ResolveType();
                        var v = Coerce(Pop(ref f), boxType);
                        Push(ref f, new Slot { Value = exp.Box(v.Value, boxType), Type = typeof(object) });
                        break;
                    }
                    case OpType.Unbox:
                    {
                        var unboxType = x.ResolveType();
                        Push(ref f, new Slot { Value = exp.Unbox(Pop(ref f).Value, unboxType), Type = unboxType, Flags = SlotFlags.Address });
                        break;
                    }
                    case OpType.Unbox_any:
                    {
                        var unboxType = x.ResolveType();
                        var v = Pop(ref f);
                        var expr = unboxType.IsValueType ? exp.Unbox(v.Value, unboxType) : exp.Cast(v.Value, unboxType);
                        Push(ref f, new Slot { Value = expr, Type = unboxType });
                        break;
                    }
                    case OpType.Castclass:
                    {
                        var castType = x.ResolveType();
                        Push(ref f, new Slot { Value = exp.Cast(Pop(ref f).Value, castType), Type = castType });
                        break;
                    }
                    case OpType.Isinst:
                    {
                        var isType = x.ResolveType();
                        var resultType = isType.IsValueType ? typeof(object) : isType;
                        Push(ref f, new Slot { Value = exp.TypeAs(Pop(ref f).Value, isType), Type = resultType });
                        break;
                    }
                    case OpType.Newarr:
                    {
                        var elemType = x.ResolveType();
                        Push(ref f, new Slot { Value = exp.Array(elemType, new[] { Index32(Pop(ref f)).Value }), Type = elemType.MakeArrayType() });
                        break;
                    }
                    case OpType.Conv_i1: Push(ref f, Conv(Pop(ref f), typeof(sbyte), false, false)); break;
                    case OpType.Conv_i2: Push(ref f, Conv(Pop(ref f), typeof(short), false, false)); break;
                    case OpType.Conv_i4: Push(ref f, Conv(Pop(ref f), typeof(int), false, false)); break;
                    case OpType.Conv_i8: Push(ref f, Conv(Pop(ref f), typeof(long), false, false)); break;
                    case OpType.Conv_i: Push(ref f, Conv(Pop(ref f), nativeInt, false, false)); break;
                    case OpType.Conv_u1: Push(ref f, Conv(Pop(ref f), typeof(byte), false, true)); break;
                    case OpType.Conv_u2: Push(ref f, Conv(Pop(ref f), typeof(ushort), false, true)); break;
                    case OpType.Conv_u4: Push(ref f, Conv(Pop(ref f), typeof(uint), false, true)); break;
                    case OpType.Conv_u8: Push(ref f, Conv(Pop(ref f), typeof(ulong), false, true)); break;
                    case OpType.Conv_u: Push(ref f, Conv(Pop(ref f), nativeUInt, false, true)); break;
                    case OpType.Conv_r4: Push(ref f, Conv(Pop(ref f), typeof(float), false, false)); break;
                    case OpType.Conv_r8: Push(ref f, Conv(Pop(ref f), typeof(double), false, false)); break;
                    case OpType.Conv_r_un: Push(ref f, Conv(Pop(ref f), typeof(double), false, true)); break;
                    case OpType.Conv_ovf_i1: Push(ref f, Conv(Pop(ref f), typeof(sbyte), true, false)); break;
                    case OpType.Conv_ovf_i2: Push(ref f, Conv(Pop(ref f), typeof(short), true, false)); break;
                    case OpType.Conv_ovf_i4: Push(ref f, Conv(Pop(ref f), typeof(int), true, false)); break;
                    case OpType.Conv_ovf_i8: Push(ref f, Conv(Pop(ref f), typeof(long), true, false)); break;
                    case OpType.Conv_ovf_i: Push(ref f, Conv(Pop(ref f), nativeInt, true, false)); break;
                    case OpType.Conv_ovf_u1: Push(ref f, Conv(Pop(ref f), typeof(byte), true, false)); break;
                    case OpType.Conv_ovf_u2: Push(ref f, Conv(Pop(ref f), typeof(ushort), true, false)); break;
                    case OpType.Conv_ovf_u4: Push(ref f, Conv(Pop(ref f), typeof(uint), true, false)); break;
                    case OpType.Conv_ovf_u8: Push(ref f, Conv(Pop(ref f), typeof(ulong), true, false)); break;
                    case OpType.Conv_ovf_u: Push(ref f, Conv(Pop(ref f), nativeUInt, true, false)); break;
                    case OpType.Conv_ovf_i1_un: Push(ref f, Conv(Pop(ref f), typeof(sbyte), true, true)); break;
                    case OpType.Conv_ovf_i2_un: Push(ref f, Conv(Pop(ref f), typeof(short), true, true)); break;
                    case OpType.Conv_ovf_i4_un: Push(ref f, Conv(Pop(ref f), typeof(int), true, true)); break;
                    case OpType.Conv_ovf_i8_un: Push(ref f, Conv(Pop(ref f), typeof(long), true, true)); break;
                    case OpType.Conv_ovf_i_un: Push(ref f, Conv(Pop(ref f), nativeInt, true, true)); break;
                    case OpType.Conv_ovf_u1_un: Push(ref f, Conv(Pop(ref f), typeof(byte), true, true)); break;
                    case OpType.Conv_ovf_u2_un: Push(ref f, Conv(Pop(ref f), typeof(ushort), true, true)); break;
                    case OpType.Conv_ovf_u4_un: Push(ref f, Conv(Pop(ref f), typeof(uint), true, true)); break;
                    case OpType.Conv_ovf_u8_un: Push(ref f, Conv(Pop(ref f), typeof(ulong), true, true)); break;
                    case OpType.Conv_ovf_u_un: Push(ref f, Conv(Pop(ref f), nativeUInt, true, true)); break;

                    //--------------- prefixes and no-ops --------------
                    case OpType.Constrained_:
                        constrainedType = x.ResolveType();
                        return false;
                    case OpType.Nop:
                    case OpType.Break:
                    case OpType.Tail_:
                    case OpType.Volatile_:
                    case OpType.Readonly_:
                    case OpType.Unaligned_:
                        break;

                    default:
                        throw new NotSupportedException("Instruction not supported: " + x);
                }

                constrainedType = null;
                return false;
            }

            void DoCall(int i, Instruction x, ref Frame f)
            {
                var constrained = constrainedType;
                constrainedType = null;

                var m = x.ResolveMethod();
                if ((m.CallingConvention & CallingConventions.VarArgs) != 0)
                    throw new NotSupportedException("Vararg calls are not supported.");

                var ps = m.GetParameters();
                // Check typeof(T) idiom before popping into T[] so we can inspect Slot.ConstObj directly
                if (m is MethodInfo mi && mi == getTypeFromHandle && ps.Length == 1)
                {
                    var handleSlot = Pop(ref f);
                    if (handleSlot.Const == ConstKind.Ref && handleSlot.ConstObj is RuntimeTypeHandle handle)
                    {
                        var resolved = System.Type.GetTypeFromHandle(handle);
                        Push(ref f, new Slot
                        {
                            Value = exp.Constant(resolved, typeof(Type)),
                            Type = typeof(Type),
                            Const = ConstKind.Ref,
                            ConstObj = resolved,
                            Flags = SlotFlags.Stable,
                        });
                        return;
                    }
                    Push(ref f, new Slot { Value = exp.Call(mi, false, default(T), new[] { handleSlot.Value }), Type = typeof(Type) });
                    return;
                }

                var margs = PopArgs(ref f, ps);
                if (m is ConstructorInfo ctor)
                {
                    if (!ctor.DeclaringType.IsValueType)
                        throw new NotSupportedException("Constructor chaining is not supported.");
                    Emit(ref f, exp.Assign(PopAddress(ref f).Value, exp.New(ctor, margs)));
                    return;
                }

                var method = (MethodInfo)m;
                T inst = default(T);
                if (!method.IsStatic)
                {
                    var instSlot = PopAny(ref f);
                    inst = instSlot.Value;
                    // Handle constrained. T callvirt when T is a value type calling a method on object/interface
                    if (constrained != null && constrained.IsValueType && !method.DeclaringType.IsValueType)
                    {
                        var overrideMethod = ResolveValueTypeOverride(constrained, method);
                        if (overrideMethod != null)
                            method = overrideMethod;
                        else
                            inst = exp.Box(inst, constrained);
                    }
                }

                var isSetter = false;
                var prop = method.IsSpecialName ? FindProperty(method, out isSetter) : null;
                if (prop != null && isSetter)
                {
                    var indexCount = margs.Length - 1;
                    T target;
                    if (indexCount == 0)
                    {
                        target = exp.Property(inst, prop);
                    }
                    else
                    {
                        var indexers = new T[indexCount];
                        Array.Copy(margs, 0, indexers, 0, indexCount);
                        target = exp.Property(inst, prop, indexers);
                    }
                    Emit(ref f, exp.Assign(target, margs[indexCount]));
                    return;
                }

                var tail = i > 0 && code[i - 1].OpCode.Type() == OpType.Tail_;
                var call = prop == null      ? exp.Call(method, tail, inst, margs) :
                           margs.Length == 0 ? exp.Property(inst, prop) :
                                               exp.Property(inst, prop, margs);
                if (method.ReturnType == typeof(void))
                {
                    Emit(ref f, call);
                }
                else
                {
                    var ret = method.ReturnType;
                    var isByRef = ret.IsByRef;
                    Push(ref f, new Slot
                    {
                        Value = call,
                        Type = isByRef ? ret.GetElementType() : ret,
                        Flags = isByRef ? SlotFlags.Address : SlotFlags.None,
                    });
                }
            }

            static MethodInfo ResolveValueTypeOverride(Type valueType, MethodInfo baseMethod)
            {
                if (baseMethod.DeclaringType.IsInterface)
                {
                    var map = valueType.GetInterfaceMap(baseMethod.DeclaringType);
                    for (int k = 0; k < map.InterfaceMethods.Length; ++k)
                        if (SameMethod(map.InterfaceMethods[k], baseMethod))
                            return map.TargetMethods[k];
                    return null;
                }
                var ps = baseMethod.GetParameters();
                var pTypes = new Type[ps.Length];
                for (int k = 0; k < ps.Length; ++k) pTypes[k] = ps[k].ParameterType;
                var m = valueType.GetMethod(baseMethod.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, pTypes, null);
                return m != null && m.GetBaseDefinition() == baseMethod.GetBaseDefinition() ? m : null;
            }

            T[] PopArgs(ref Frame f, ParameterInfo[] ps)
            {
                if (ps.Length == 0) return Array.Empty<T>();
                var margs = new T[ps.Length];
                for (int k = ps.Length - 1; k >= 0; --k)
                {
                    var type = ps[k].ParameterType;
                    if (!type.IsByRef)
                    {
                        margs[k] = Coerce(Pop(ref f), type).Value;
                        continue;
                    }
                    var addr = PopAddress(ref f);
                    var elemType = type.GetElementType();
                    if (addr.Type != elemType && addr.Type != type)
                        throw new NotSupportedException($"Passing a {addr.Type} location as a {type} argument is not supported.");
                    margs[k] = addr.Value;
                }
                return margs;
            }

            T BranchTest(Instruction x, ref Frame f, out bool takenWhenTrue)
            {
                switch (x.OpCode.Type())
                {
                    case OpType.Brtrue:
                    case OpType.Brtrue_s:
                        takenWhenTrue = true;
                        return ToBool(Pop(ref f)).Value;
                    case OpType.Brfalse:
                    case OpType.Brfalse_s:
                        takenWhenTrue = false;
                        return ToBool(Pop(ref f)).Value;
                    default:
                    {
                        takenWhenTrue = true;
                        var r = Pop(ref f);
                        var l = Pop(ref f);
                        return BranchCompare(x.OpCode.Type(), l, r).Value;
                    }
                }
            }

            static PropertyInfo FindProperty(MethodInfo m, out bool isSetter)
            {
                isSetter = false;
                if (!m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                    return null;
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var p in m.DeclaringType.GetProperties(all))
                {
                    if (SameMethod(p.GetGetMethod(true), m))
                        return p;
                    if (SameMethod(p.GetSetMethod(true), m))
                    {
                        isSetter = true;
                        return p;
                    }
                }
                return null;
            }

            static bool SameMethod(MethodInfo a, MethodInfo b) =>
                a != null && a.MetadataToken == b.MetadataToken && a.Module == b.Module;
            #endregion

            #region Arena-Backed Frame Operations
            Frame NewFrame(int stackBase, int stmtBase) =>
                new Frame { StackBase = stackBase, StackTop = stackBase, Low = 0, Clean = 0, StmtBase = stmtBase, StmtTop = stmtBase };

            Frame ForkFrame(ref Frame parent)
            {
                int depth = parent.Depth;
                int stackBase = parent.StackTop;
                EnsureStackCapacity(stackBase + depth + 8);
                if (depth > 0)
                    Array.Copy(stackBuf, parent.StackBase, stackBuf, stackBase, depth);
                return new Frame
                {
                    StackBase = stackBase,
                    StackTop = stackBase + depth,
                    Low = depth,
                    Clean = depth,
                    StmtBase = parent.StmtTop,
                    StmtTop = parent.StmtTop,
                };
            }

            void EnsureStackCapacity(int required)
            {
                if (required <= stackBuf.Length) return;
                int next = stackBuf.Length * 2;
                while (next < required) next *= 2;
                Array.Resize(ref stackBuf, next);
            }

            void EnsureStmtCapacity(int required)
            {
                if (required <= stmtBuf.Length) return;
                int next = stmtBuf.Length * 2;
                while (next < required) next *= 2;
                Array.Resize(ref stmtBuf, next);
            }

            void Push(ref Frame f, Slot s)
            {
                if (f.StackTop == stackBuf.Length)
                    EnsureStackCapacity(f.StackTop + 1);
                stackBuf[f.StackTop++] = s;
            }

            void PushInt(ref Frame f, int v) =>
                Push(ref f, new Slot { Value = exp.Constant(v), Type = typeof(int), Const = ConstKind.Int32, ConstVal = new Operand(v), Flags = SlotFlags.Stable });

            Slot PopAny(ref Frame f)
            {
                if (f.StackTop <= f.StackBase)
                    throw new InvalidProgramException("Evaluation stack underflow.");
                var s = stackBuf[--f.StackTop];
                int d = f.Depth;
                if (f.Low > d) f.Low = d;
                if (f.Clean > d) f.Clean = d;
                return s;
            }

            Slot Pop(ref Frame f)
            {
                var s = PopAny(ref f);
                if (s.IsAddress)
                    throw new NotSupportedException("Using a managed address as a value (pointer arithmetic, comparison or conversion) is not supported.");
                return s;
            }

            Slot PopAddress(ref Frame f)
            {
                var s = PopAny(ref f);
                if (!s.IsAddress)
                    throw new NotSupportedException("Indirect access through a computed pointer is not supported.");
                return s;
            }

            static void Truncate(ref Frame f, int depth)
            {
                f.StackTop = f.StackBase + depth;
                if (f.Low > depth) f.Low = depth;
                if (f.Clean > depth) f.Clean = depth;
            }

            void EmitRaw(ref Frame f, T stmt)
            {
                if (f.StmtTop == stmtBuf.Length)
                    EnsureStmtCapacity(f.StmtTop + 1);
                stmtBuf[f.StmtTop++] = stmt;
            }

            void Emit(ref Frame f, T stmt)
            {
                Spill(ref f);
                EmitRaw(ref f, stmt);
            }

            void Spill(ref Frame f)
            {
                int depth = f.Depth;
                for (int j = f.Clean; j < depth; ++j)
                {
                    int idx = f.StackBase + j;
                    if (!stackBuf[idx].IsStable)
                        stackBuf[idx] = Stabilize(ref f, stackBuf[idx]);
                }
                f.Clean = depth;
            }

            Slot Stabilize(ref Frame f, Slot s)
            {
                if (!s.IsAddress)
                {
                    var t = Temp(s.Type);
                    EmitRaw(ref f, exp.Assign(t, s.Value));
                    return new Slot { Value = t, Type = s.Type, Flags = SlotFlags.Stable | (s.Flags & SlotFlags.FromCmp) };
                }
                if (s.IsElement)
                {
                    var arr = s.AddrBaseStable ? s.AddrBase : SpillValue(ref f, s.AddrBase, s.AddrBaseType);
                    var idx = s.AddrIndexStable ? s.AddrIndex : SpillValue(ref f, s.AddrIndex, s.AddrIndexType);
                    return new Slot { Value = exp.AddressOf(exp.ArrayGet(arr, idx)), Type = s.Type, Flags = SlotFlags.Stable | SlotFlags.Address };
                }
                if (s.IsFieldAddr && s.AddrField != null && !s.AddrBaseType.IsValueType)
                {
                    var obj = s.AddrBaseStable ? s.AddrBase : SpillValue(ref f, s.AddrBase, s.AddrBaseType);
                    return new Slot { Value = exp.AddressOf(exp.Field(s.AddrField, obj)), Type = s.Type, Flags = SlotFlags.Stable | SlotFlags.Address };
                }
                throw new NotSupportedException("A computed address cannot be preserved across a statement.");
            }

            T SpillValue(ref Frame f, T value, Type type)
            {
                var t = Temp(type);
                EmitRaw(ref f, exp.Assign(t, value));
                return t;
            }

            void Discard(ref Frame f)
            {
                for (int j = f.StackBase; j < f.StackTop; ++j)
                    if (!stackBuf[j].IsStable)
                        EmitRaw(ref f, stackBuf[j].Value);
                Truncate(ref f, 0);
            }

            T MaterializeBlock(int start, int length)
            {
                if (length == 0) return exp.Block(Array.Empty<T>());
                if (length == 1) return stmtBuf[start];
                var arr = new T[length];
                Array.Copy(stmtBuf, start, arr, 0, length);
                return exp.Block(arr);
            }

            T Temp(Type type) => exp.Param("_t" + tempCount++, type);
            #endregion

            #region Decoder-Side CIL Type & Constant Tracking (Optimization 3)
            Slot Coerce(Slot s, Type target)
            {
                if (target.IsByRef)
                    target = target.GetElementType();
                if (s.Type == target || target == typeof(void))
                    return s;
                if (s.IsConstant && TryConvertConstant(s, target, out var folded))
                    return folded;
                if (target == typeof(bool))
                    return ToBool(s);
                if (s.Type == typeof(bool) && (IsIntegral(target) || target.IsEnum))
                    return new Slot { Value = exp.If(s.Value, MakeConst(1, target).Value, MakeConst(0, target).Value), Type = target };
                var val = s.Value;
                if (s.Type.IsEnum && target.IsEnum)
                    val = exp.Cast(val, Enum.GetUnderlyingType(s.Type));
                return new Slot { Value = exp.Cast(val, target), Type = target };
            }

            Slot ToBool(Slot s)
            {
                if (s.Type == typeof(bool))
                    return s;
                if (s.IsConstant)
                {
                    bool b = s.Const == ConstKind.Null    ? false :
                             s.Const == ConstKind.Bool    ? s.ConstVal.Int32 != 0 :
                             s.Const == ConstKind.Int32   ? s.ConstVal.Int32 != 0 :
                             s.Const == ConstKind.Int64   ? s.ConstVal.Int64 != 0 :
                             s.Const == ConstKind.Float32 ? s.ConstVal.Float32 != 0 :
                             s.Const == ConstKind.Float64 ? s.ConstVal.Float64 != 0 :
                                                            s.ConstObj != null;
                    return MakeBoolConst(b);
                }
                var zero = IsReference(s.Type) ? exp.Constant(null, s.Type) : MakeConst(0, s.Type).Value;
                return new Slot { Value = exp.NotEqual(s.Value, zero), Type = typeof(bool) };
            }

            Slot MakeBoolConst(bool b) =>
                new Slot { Value = exp.Constant(b), Type = typeof(bool), Const = ConstKind.Bool, ConstVal = new Operand(b ? 1 : 0), Flags = SlotFlags.Stable };

            Slot MakeConst(int value, Type type)
            {
                if (type == typeof(bool) || IsIntegral(type) || type.IsEnum)
                    return SlotFromBits(value, type);
                var intSlot = new Slot { Value = exp.Constant(value), Type = typeof(int), Const = ConstKind.Int32, ConstVal = new Operand(value), Flags = SlotFlags.Stable };
                if (TryConvertConstant(intSlot, type, out var res))
                    return res;
                var obj = Activator.CreateInstance(type);
                return new Slot { Value = exp.Constant(obj, type), Type = type, Const = ConstKind.Ref, ConstObj = obj, Flags = SlotFlags.Stable };
            }

            Slot Normalize(Slot s)
            {
                var t = s.Type;
                if (t == typeof(IntPtr))
                    return Reinterpret(s, nativeInt);
                if (t == typeof(UIntPtr))
                    return Reinterpret(s, nativeUInt);
                if (t.IsEnum)
                {
                    t = Enum.GetUnderlyingType(t);
                    s = Reinterpret(s, t);
                }
                if (t == typeof(bool))
                {
                    if (s.IsConstant)
                        return MakeConst(s.ConstVal.Int32 != 0 ? 1 : 0, typeof(int));
                    return new Slot { Value = exp.If(s.Value, exp.Constant(1), exp.Constant(0)), Type = typeof(int) };
                }
                if (t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort) || t == typeof(char))
                    return Reinterpret(s, typeof(int));
                return s;
            }

            Slot Reinterpret(Slot s, Type target)
            {
                if (s.Type == target)
                    return s;
                if (s.IsConstant && TryConvertConstant(s, target, out var folded))
                    return folded;
                return new Slot { Value = exp.Cast(s.Value, target), Type = target };
            }

            Slot AsSigned(Slot s) =>
                s.Type == typeof(uint)  ? Reinterpret(s, typeof(int)) :
                s.Type == typeof(ulong) ? Reinterpret(s, typeof(long)) :
                                          s;

            Slot AsUnsigned(Slot s) =>
                s.Type == typeof(int)  ? Reinterpret(s, typeof(uint)) :
                s.Type == typeof(long) ? Reinterpret(s, typeof(ulong)) :
                                         s;

            void Reconcile(ref Slot l, ref Slot r)
            {
                if (l.Type == r.Type) return;
                if (IsIntegral(l.Type) && IsIntegral(r.Type))
                {
                    if (SizeOf(l.Type) == SizeOf(r.Type))
                        r = Reinterpret(r, l.Type);
                    else if (SizeOf(l.Type) < SizeOf(r.Type))
                        l = Reinterpret(Reinterpret(AsSigned(l), typeof(long)), r.Type);
                    else
                        r = Reinterpret(Reinterpret(AsSigned(r), typeof(long)), l.Type);
                }
                else if (IsFloat(l.Type) && IsFloat(r.Type))
                {
                    if (l.Type == typeof(float)) l = Reinterpret(l, typeof(double));
                    else r = Reinterpret(r, typeof(double));
                }
            }

            Slot Arith(OpType op, Slot l, Slot r)
            {
                if (l.Type == typeof(bool) && r.Type == typeof(bool))
                {
                    switch (op)
                    {
                        case OpType.And: return new Slot { Value = exp.And(l.Value, r.Value), Type = typeof(bool) };
                        case OpType.Or: return new Slot { Value = exp.Or(l.Value, r.Value), Type = typeof(bool) };
                        case OpType.Xor: return new Slot { Value = exp.Xor(l.Value, r.Value), Type = typeof(bool) };
                    }
                }
                l = Normalize(l);
                r = Normalize(r);
                Reconcile(ref l, ref r);
                var integral = IsIntegral(l.Type);
                switch (op)
                {
                    case OpType.Add: return new Slot { Value = exp.Add(l.Value, r.Value), Type = l.Type };
                    case OpType.Sub: return new Slot { Value = exp.Subtract(l.Value, r.Value), Type = l.Type };
                    case OpType.Mul: return new Slot { Value = exp.Multiply(l.Value, r.Value), Type = l.Type };
                    case OpType.And: return new Slot { Value = exp.And(l.Value, r.Value), Type = l.Type };
                    case OpType.Or: return new Slot { Value = exp.Or(l.Value, r.Value), Type = l.Type };
                    case OpType.Xor: return new Slot { Value = exp.Xor(l.Value, r.Value), Type = l.Type };
                    case OpType.Div:
                        if (integral) { l = AsSigned(l); r = AsSigned(r); }
                        return new Slot { Value = exp.Divide(l.Value, r.Value), Type = l.Type };
                    case OpType.Div_un:
                        l = AsUnsigned(l); r = AsUnsigned(r);
                        return new Slot { Value = exp.Divide(l.Value, r.Value), Type = l.Type };
                    case OpType.Rem:
                        if (integral) { l = AsSigned(l); r = AsSigned(r); }
                        return new Slot { Value = exp.Modulo(l.Value, r.Value), Type = l.Type };
                    case OpType.Rem_un:
                        l = AsUnsigned(l); r = AsUnsigned(r);
                        return new Slot { Value = exp.Modulo(l.Value, r.Value), Type = l.Type };
                    case OpType.Add_ovf:
                        l = AsSigned(l); r = AsSigned(r);
                        return new Slot { Value = exp.AddOverflow(l.Value, r.Value), Type = l.Type };
                    case OpType.Add_ovf_un:
                        l = AsUnsigned(l); r = AsUnsigned(r);
                        return new Slot { Value = exp.AddOverflow(l.Value, r.Value), Type = l.Type };
                    case OpType.Sub_ovf:
                        l = AsSigned(l); r = AsSigned(r);
                        return new Slot { Value = exp.SubtractOverflow(l.Value, r.Value), Type = l.Type };
                    case OpType.Sub_ovf_un:
                        l = AsUnsigned(l); r = AsUnsigned(r);
                        return new Slot { Value = exp.SubtractOverflow(l.Value, r.Value), Type = l.Type };
                    case OpType.Mul_ovf:
                        l = AsSigned(l); r = AsSigned(r);
                        return new Slot { Value = exp.MultiplyOverflow(l.Value, r.Value), Type = l.Type };
                    case OpType.Mul_ovf_un:
                        l = AsUnsigned(l); r = AsUnsigned(r);
                        return new Slot { Value = exp.MultiplyOverflow(l.Value, r.Value), Type = l.Type };
                    default: throw new ArgumentOutOfRangeException(nameof(op));
                }
            }

            Slot Shift(OpType op, Slot value, Slot amount)
            {
                value = Normalize(value);
                amount = Reinterpret(AsSigned(Normalize(amount)), typeof(int));
                switch (op)
                {
                    case OpType.Shl:
                        return new Slot { Value = exp.LeftShift(value.Value, amount.Value), Type = value.Type };
                    case OpType.Shr:
                        value = AsSigned(value);
                        return new Slot { Value = exp.RightShift(value.Value, amount.Value), Type = value.Type };
                    default:
                        value = AsUnsigned(value);
                        return new Slot { Value = exp.RightShift(value.Value, amount.Value), Type = value.Type };
                }
            }

            Slot Negate(Slot s)
            {
                s = Normalize(s);
                if (IsIntegral(s.Type)) s = AsSigned(s);
                return new Slot { Value = exp.Negate(s.Value), Type = s.Type };
            }

            Slot Conv(Slot s, Type target, bool overflow, bool unsignedSource)
            {
                s = Normalize(s);
                if (IsIntegral(s.Type))
                    s = unsignedSource ? AsUnsigned(s) : AsSigned(s);
                if (s.IsConstant)
                {
                    var folded = overflow ? TryConvertChecked(s, target, out var res) : TryConvertConstant(s, target, out res);
                    if (folded) return res;
                }
                return new Slot { Value = overflow ? exp.CastOverflow(s.Value, target) : exp.Cast(s.Value, target), Type = target };
            }

            Slot Index32(Slot s) => Reinterpret(AsSigned(Normalize(s)), typeof(int));

            T SwitchValue(Slot s) => Reinterpret(AsSigned(Normalize(s)), typeof(int)).Value;

            Slot BranchCompare(OpType op, Slot l, Slot r)
            {
                switch (op)
                {
                    case OpType.Beq: case OpType.Beq_s: return Compare(Cmp.Eq, l, r, false);
                    case OpType.Bne_un: case OpType.Bne_un_s: return Compare(Cmp.Ne, l, r, true);
                    case OpType.Bge: case OpType.Bge_s: return Compare(Cmp.Ge, l, r, false);
                    case OpType.Bge_un: case OpType.Bge_un_s: return Compare(Cmp.Ge, l, r, true);
                    case OpType.Bgt: case OpType.Bgt_s: return Compare(Cmp.Gt, l, r, false);
                    case OpType.Bgt_un: case OpType.Bgt_un_s: return Compare(Cmp.Gt, l, r, true);
                    case OpType.Ble: case OpType.Ble_s: return Compare(Cmp.Le, l, r, false);
                    case OpType.Ble_un: case OpType.Ble_un_s: return Compare(Cmp.Le, l, r, true);
                    case OpType.Blt: case OpType.Blt_s: return Compare(Cmp.Lt, l, r, false);
                    default: return Compare(Cmp.Lt, l, r, true);
                }
            }

            Slot Compare(Cmp kind, Slot l, Slot r, bool un)
            {
                if (IsReference(l.Type) || IsReference(r.Type))
                {
                    if (!IsReference(l.Type)) l = new Slot { Value = exp.Cast(l.Value, typeof(object)), Type = typeof(object) };
                    if (!IsReference(r.Type)) r = new Slot { Value = exp.Cast(r.Value, typeof(object)), Type = typeof(object) };
                    if (l.IsNull && !r.IsNull)
                        l = new Slot { Value = exp.Constant(null, r.Type), Type = r.Type, Const = ConstKind.Null, Flags = SlotFlags.Stable };
                    else if (r.IsNull && !l.IsNull)
                        r = new Slot { Value = exp.Constant(null, l.Type), Type = l.Type, Const = ConstKind.Null, Flags = SlotFlags.Stable };
                    else if (!l.IsNull && (l.Type != r.Type || HasEqualityOperator(l.Type)))
                    {
                        l = new Slot { Value = exp.Cast(l.Value, typeof(object)), Type = typeof(object) };
                        r = new Slot { Value = exp.Cast(r.Value, typeof(object)), Type = typeof(object) };
                    }
                    switch (kind)
                    {
                        case Cmp.Eq:
                            return new Slot { Value = exp.Equal(l.Value, r.Value), Type = typeof(bool), Flags = SlotFlags.FromCmp };
                        case Cmp.Ne:
                        case Cmp.Gt when un:
                            return new Slot { Value = exp.NotEqual(l.Value, r.Value), Type = typeof(bool), Flags = SlotFlags.FromCmp };
                        default:
                            throw new NotSupportedException("Ordered comparison of object references is not supported.");
                    }
                }

                // Keep comparisons against bool, char, and enum constants readable
                if (r.IsConstant && !l.IsConstant && IsSmall(l.Type) && TryConvertExact(r, l.Type, out var rc))
                    r = rc;
                else if (l.IsConstant && !r.IsConstant && IsSmall(r.Type) && TryConvertExact(l, r.Type, out var lc))
                    l = lc;

                if (l.Type == r.Type && IsSmall(l.Type) && (kind == Cmp.Eq || kind == Cmp.Ne))
                    return new Slot { Value = kind == Cmp.Eq ? exp.Equal(l.Value, r.Value) : exp.NotEqual(l.Value, r.Value), Type = typeof(bool), Flags = SlotFlags.FromCmp };

                l = Normalize(l);
                r = Normalize(r);
                Reconcile(ref l, ref r);
                if (IsFloat(l.Type))
                {
                    T fe;
                    switch (kind)
                    {
                        case Cmp.Eq: fe = exp.Equal(l.Value, r.Value); break;
                        case Cmp.Ne: fe = exp.NotEqual(l.Value, r.Value); break;
                        case Cmp.Lt: fe = un ? exp.Not(exp.GreaterThanOrEqual(l.Value, r.Value)) : exp.LessThan(l.Value, r.Value); break;
                        case Cmp.Le: fe = un ? exp.Not(exp.GreaterThan(l.Value, r.Value)) : exp.LessThanOrEqual(l.Value, r.Value); break;
                        case Cmp.Gt: fe = un ? exp.Not(exp.LessThanOrEqual(l.Value, r.Value)) : exp.GreaterThan(l.Value, r.Value); break;
                        default:     fe = un ? exp.Not(exp.LessThan(l.Value, r.Value)) : exp.GreaterThanOrEqual(l.Value, r.Value); break;
                    }
                    return new Slot { Value = fe, Type = typeof(bool), Flags = SlotFlags.FromCmp };
                }

                if (kind != Cmp.Eq && kind != Cmp.Ne)
                {
                    l = un ? AsUnsigned(l) : AsSigned(l);
                    r = un ? AsUnsigned(r) : AsSigned(r);
                }
                T ie;
                switch (kind)
                {
                    case Cmp.Eq: ie = exp.Equal(l.Value, r.Value); break;
                    case Cmp.Ne: ie = exp.NotEqual(l.Value, r.Value); break;
                    case Cmp.Lt: ie = exp.LessThan(l.Value, r.Value); break;
                    case Cmp.Le: ie = exp.LessThanOrEqual(l.Value, r.Value); break;
                    case Cmp.Gt: ie = exp.GreaterThan(l.Value, r.Value); break;
                    default:     ie = exp.GreaterThanOrEqual(l.Value, r.Value); break;
                }
                return new Slot { Value = ie, Type = typeof(bool), Flags = SlotFlags.FromCmp };
            }

            static bool IsConstBool(Slot s, bool expected)
            {
                if (s.Const == ConstKind.Bool) return (s.ConstVal.Int32 != 0) == expected;
                if (s.Const == ConstKind.Int32) return s.ConstVal.Int32 == (expected ? 1 : 0);
                return false;
            }

            static bool IsBoolCompatible(Slot s) =>
                s.Type == typeof(bool) || (s.Const == ConstKind.Int32 && (s.ConstVal.Int32 == 0 || s.ConstVal.Int32 == 1));

            /// <summary>
            /// Unifies the CLR types of two merged stack slots without corrupting arbitrary
            /// integers into booleans (e.g. `flag ? (a == b ? 1 : 0) : intVar` stays `int`).
            /// </summary>
            static Type UnifyTypes(Slot a, Slot b)
            {
                if (a.IsNull) return b.Type;
                if (b.IsNull) return a.Type;
                if (a.Type == b.Type) return a.Type;
                if ((a.Type == typeof(bool) && IsBoolCompatible(b)) || (b.Type == typeof(bool) && IsBoolCompatible(a)))
                    return typeof(bool);
                if (a.Type == typeof(bool) && IsIntegral(b.Type)) return b.Type;
                if (b.Type == typeof(bool) && IsIntegral(a.Type)) return a.Type;
                if (!a.IsConstant && b.IsConstant && CanHoldConstant(a.Type, b)) return a.Type;
                if (!b.IsConstant && a.IsConstant && CanHoldConstant(b.Type, a)) return b.Type;
                return UnifyTwoNonNullTypes(a.Type, b.Type);
            }

            static Type UnifyTypes(Type acc, Slot b)
            {
                if (b.IsNull) return acc;
                if (acc == b.Type) return acc;
                if (acc == typeof(bool) && IsBoolCompatible(b)) return typeof(bool);
                if (acc == typeof(bool) && IsIntegral(b.Type)) return b.Type;
                if (b.Type == typeof(bool) && IsIntegral(acc)) return acc;
                return UnifyTwoNonNullTypes(acc, b.Type);
            }

            static Type UnifyTwoNonNullTypes(Type ta, Type tb)
            {
                if (ta == tb) return ta;
                if (!ta.IsValueType && !tb.IsValueType)
                {
                    if (ta.IsAssignableFrom(tb)) return ta;
                    if (tb.IsAssignableFrom(ta)) return tb;
                    return typeof(object);
                }
                if (IsIntegral(ta) && IsIntegral(tb))
                    return SizeOf(ta) >= SizeOf(tb) ? ta : tb;
                if (IsFloat(ta) && IsFloat(tb))
                    return typeof(double);
                return typeof(object);
            }

            static bool CanHoldConstant(Type target, Slot c) =>
                (target == typeof(bool) && IsBoolCompatible(c))
                || (IsIntegral(target) && (c.Const == ConstKind.Int32 || c.Const == ConstKind.Int64))
                || (target.IsEnum && (c.Const == ConstKind.Int32 || c.Const == ConstKind.Int64));

            static bool IsReference(Type t) => !t.IsValueType || Nullable.GetUnderlyingType(t) != null;

            static bool IsSmall(Type t) =>
                t == typeof(bool) || t == typeof(char) || t.IsEnum || t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort);

            static bool IsIntegral(Type t) =>
                t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort) || t == typeof(char) ||
                t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong);

            static bool IsFloat(Type t) => t == typeof(float) || t == typeof(double);

            static int SizeOf(Type t) =>
                t == typeof(sbyte) || t == typeof(byte) || t == typeof(bool)   ? 1 :
                t == typeof(short) || t == typeof(ushort) || t == typeof(char) ? 2 :
                t == typeof(int) || t == typeof(uint) || t == typeof(float)    ? 4 :
                                                                                 8;

            static bool HasEqualityOperator(Type t)
            {
                try
                {
                    return t.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static, null, new[] { t, t }, null) != null;
                }
                catch (AmbiguousMatchException)
                {
                    return true;
                }
            }

            static bool TryGetBits(Slot s, out long bits, out bool unsigned)
            {
                unsigned = false;
                switch (s.Const)
                {
                    case ConstKind.Bool:
                        bits = s.ConstVal.Int32 != 0 ? 1 : 0;
                        return true;
                    case ConstKind.Int32:
                        if (s.Type == typeof(uint)) { bits = (uint)s.ConstVal.Int32; unsigned = true; return true; }
                        if (s.Type == typeof(byte) || s.Type == typeof(ushort) || s.Type == typeof(char)) { bits = s.ConstVal.Int32; unsigned = true; return true; }
                        bits = s.ConstVal.Int32;
                        return true;
                    case ConstKind.Int64:
                        if (s.Type == typeof(ulong)) { bits = s.ConstVal.Int64; unsigned = true; return true; }
                        bits = s.ConstVal.Int64;
                        return true;
                    case ConstKind.Ref when s.ConstObj is Enum e:
                        var u = Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()));
                        switch (u)
                        {
                            case sbyte x: bits = x; return true;
                            case short x: bits = x; return true;
                            case int x: bits = x; return true;
                            case long x: bits = x; return true;
                            case byte x: bits = x; unsigned = true; return true;
                            case ushort x: bits = x; unsigned = true; return true;
                            case uint x: bits = x; unsigned = true; return true;
                            case ulong x: bits = unchecked((long)x); unsigned = true; return true;
                        }
                        break;
                }
                bits = 0;
                return false;
            }

            Slot SlotFromBits(long bits, Type target)
            {
                unchecked
                {
                    if (target == typeof(bool)) return MakeBoolConst(bits != 0);
                    if (target == typeof(sbyte)) { var v = (sbyte)bits; return new Slot { Value = exp.Constant(v), Type = target, Const = ConstKind.Int32, ConstVal = new Operand((int)v), Flags = SlotFlags.Stable }; }
                    if (target == typeof(byte)) { var v = (byte)bits; return new Slot { Value = exp.Constant(v), Type = target, Const = ConstKind.Int32, ConstVal = new Operand((int)v), Flags = SlotFlags.Stable }; }
                    if (target == typeof(short)) { var v = (short)bits; return new Slot { Value = exp.Constant(v), Type = target, Const = ConstKind.Int32, ConstVal = new Operand((int)v), Flags = SlotFlags.Stable }; }
                    if (target == typeof(ushort)) { var v = (ushort)bits; return new Slot { Value = exp.Constant(v), Type = target, Const = ConstKind.Int32, ConstVal = new Operand((int)v), Flags = SlotFlags.Stable }; }
                    if (target == typeof(char)) { var v = (char)bits; return new Slot { Value = exp.Constant(v), Type = target, Const = ConstKind.Int32, ConstVal = new Operand((int)v), Flags = SlotFlags.Stable }; }
                    if (target == typeof(int)) { var v = (int)bits; return new Slot { Value = exp.Constant(v), Type = target, Const = ConstKind.Int32, ConstVal = new Operand(v), Flags = SlotFlags.Stable }; }
                    if (target == typeof(uint)) { var v = (uint)bits; return new Slot { Value = exp.Constant(v), Type = target, Const = ConstKind.Int32, ConstVal = new Operand(v), Flags = SlotFlags.Stable }; }
                    if (target == typeof(long)) return new Slot { Value = exp.Constant(bits), Type = target, Const = ConstKind.Int64, ConstVal = new Operand(bits), Flags = SlotFlags.Stable };
                    if (target == typeof(ulong)) { var v = (ulong)bits; return new Slot { Value = exp.Constant(v), Type = target, Const = ConstKind.Int64, ConstVal = new Operand(bits), Flags = SlotFlags.Stable }; }
                    var e = Enum.ToObject(target, BoxBits(bits, Enum.GetUnderlyingType(target)));
                    return new Slot { Value = exp.Constant(e, target), Type = target, Const = ConstKind.Ref, ConstObj = e, Flags = SlotFlags.Stable };
                }
            }

            static object BoxBits(long bits, Type t)
            {
                unchecked
                {
                    if (t == typeof(sbyte)) return (sbyte)bits;
                    if (t == typeof(byte)) return (byte)bits;
                    if (t == typeof(short)) return (short)bits;
                    if (t == typeof(ushort)) return (ushort)bits;
                    if (t == typeof(int)) return (int)bits;
                    if (t == typeof(uint)) return (uint)bits;
                    if (t == typeof(long)) return bits;
                    return (ulong)bits;
                }
            }

            bool TryConvertConstant(Slot s, Type target, out Slot result)
            {
                result = default(Slot);
                if (s.Const == ConstKind.Null)
                {
                    if (!IsReference(target)) return false;
                    result = new Slot { Value = exp.Constant(null, target), Type = target, Const = ConstKind.Null, Flags = SlotFlags.Stable };
                    return true;
                }
                if (s.Type == target)
                {
                    result = s;
                    return true;
                }
                if (TryGetBits(s, out var bits, out var unsigned))
                {
                    if (target == typeof(bool) || IsIntegral(target) || target.IsEnum)
                    {
                        result = SlotFromBits(bits, target);
                        return true;
                    }
                    if (target == typeof(double))
                    {
                        var d = unsigned ? (double)(ulong)bits : (double)bits;
                        result = new Slot { Value = exp.Constant(d), Type = typeof(double), Const = ConstKind.Float64, ConstVal = new Operand(d), Flags = SlotFlags.Stable };
                        return true;
                    }
                    if (target == typeof(float))
                    {
                        var fl = unsigned ? (float)(ulong)bits : (float)bits;
                        result = new Slot { Value = exp.Constant(fl), Type = typeof(float), Const = ConstKind.Float32, ConstVal = new Operand(fl), Flags = SlotFlags.Stable };
                        return true;
                    }
                }
                else if (s.Const == ConstKind.Float32 || s.Const == ConstKind.Float64)
                {
                    var d = s.Const == ConstKind.Float32 ? (double)s.ConstVal.Float32 : s.ConstVal.Float64;
                    if (target == typeof(double))
                    {
                        result = new Slot { Value = exp.Constant(d), Type = typeof(double), Const = ConstKind.Float64, ConstVal = new Operand(d), Flags = SlotFlags.Stable };
                        return true;
                    }
                    if (target == typeof(float))
                    {
                        var fl = (float)d;
                        result = new Slot { Value = exp.Constant(fl), Type = typeof(float), Const = ConstKind.Float32, ConstVal = new Operand(fl), Flags = SlotFlags.Stable };
                        return true;
                    }
                    return IsIntegral(target) && TryConvertChecked(s, target, out result);
                }
                if (s.Const == ConstKind.Ref && !target.IsValueType && target.IsAssignableFrom(s.Type))
                {
                    result = new Slot { Value = exp.Constant(s.ConstObj, target), Type = target, Const = ConstKind.Ref, ConstObj = s.ConstObj, Flags = SlotFlags.Stable };
                    return true;
                }
                return false;
            }

            bool TryConvertExact(Slot s, Type target, out Slot result)
            {
                if (!TryConvertConstant(s, target, out result)
                    || !TryGetBits(s, out var a, out var au)
                    || !TryGetBits(result, out var b, out var bu))
                    return false;
                if (target == typeof(bool))
                    return a == 0 || a == 1;
                return a == b && (au == bu || a >= 0);
            }

            bool TryConvertChecked(Slot s, Type target, out Slot result)
            {
                result = default(Slot);
                decimal value;
                if (TryGetBits(s, out var bits, out var unsigned))
                {
                    value = unsigned ? (decimal)(ulong)bits : bits;
                }
                else if (s.Const == ConstKind.Float32 || s.Const == ConstKind.Float64)
                {
                    var d = s.Const == ConstKind.Float32 ? (double)s.ConstVal.Float32 : s.ConstVal.Float64;
                    if (double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > 1e20)
                        return false;
                    if (target == typeof(double) || target == typeof(float))
                        return TryConvertConstant(s, target, out result);
                    value = (decimal)Math.Truncate(d);
                }
                else return false;

                if (target == typeof(double) || target == typeof(float))
                    return TryConvertConstant(s, target, out result);

                decimal min, max;
                if (target == typeof(sbyte)) { min = sbyte.MinValue; max = sbyte.MaxValue; }
                else if (target == typeof(byte)) { min = byte.MinValue; max = byte.MaxValue; }
                else if (target == typeof(short)) { min = short.MinValue; max = short.MaxValue; }
                else if (target == typeof(ushort) || target == typeof(char)) { min = ushort.MinValue; max = ushort.MaxValue; }
                else if (target == typeof(int)) { min = int.MinValue; max = int.MaxValue; }
                else if (target == typeof(uint)) { min = uint.MinValue; max = uint.MaxValue; }
                else if (target == typeof(long)) { min = long.MinValue; max = long.MaxValue; }
                else if (target == typeof(ulong)) { min = ulong.MinValue; max = ulong.MaxValue; }
                else return false;

                if (value < min || value > max) return false;
                result = SlotFromBits(value < 0 ? (long)value : unchecked((long)(ulong)value), target);
                return true;
            }
            #endregion

            #region Instruction Operands
            int Index(IL.Label label)
            {
                if (label.Offset >= byteLength)
                    return count;
                var idx = code.GetIndex(label);
                if (code.GetLabel(idx) != label)
                    throw new InvalidProgramException($"Branch into the middle of an instruction at {label}.");
                return idx;
            }

            int Target(Instruction x) => Index(x.Operand.Label);

            string LabelOfBlock(int b) => b >= 0 && b < blockCount ? code.GetLabel(blockStart[b]).ToString() : "the end of the method";

            static int ArgIndex(Instruction x)
            {
                switch (x.OpCode.Type())
                {
                    case OpType.Ldarg_0: return 0;
                    case OpType.Ldarg_1: return 1;
                    case OpType.Ldarg_2: return 2;
                    case OpType.Ldarg_3: return 3;
                    case OpType.Ldarg_s:
                    case OpType.Ldarga_s:
                    case OpType.Starg_s: return x.Operand.UInt8;
                    default: return x.Operand.UInt16;
                }
            }

            static int LocalIndex(Instruction x)
            {
                switch (x.OpCode.Type())
                {
                    case OpType.Ldloc_0: case OpType.Stloc_0: return 0;
                    case OpType.Ldloc_1: case OpType.Stloc_1: return 1;
                    case OpType.Ldloc_2: case OpType.Stloc_2: return 2;
                    case OpType.Ldloc_3: case OpType.Stloc_3: return 3;
                    case OpType.Ldloc_s:
                    case OpType.Ldloca_s:
                    case OpType.Stloc_s: return x.Operand.UInt8;
                    default: return x.Operand.UInt16;
                }
            }

            static Type ElemTypeFor(OpType op)
            {
                switch (op)
                {
                    case OpType.Ldelem_i1: return typeof(sbyte);
                    case OpType.Ldelem_u1: return typeof(byte);
                    case OpType.Ldelem_i2: return typeof(short);
                    case OpType.Ldelem_u2: return typeof(ushort);
                    case OpType.Ldelem_i4: return typeof(int);
                    case OpType.Ldelem_u4: return typeof(uint);
                    case OpType.Ldelem_i8: return typeof(long);
                    case OpType.Ldelem_i: return nativeInt;
                    case OpType.Ldelem_r4: return typeof(float);
                    case OpType.Ldelem_r8: return typeof(double);
                    default: return typeof(object);
                }
            }

            static Type IndTypeFor(OpType op)
            {
                switch (op)
                {
                    case OpType.Ldind_i1: return typeof(sbyte);
                    case OpType.Ldind_u1: return typeof(byte);
                    case OpType.Ldind_i2: return typeof(short);
                    case OpType.Ldind_u2: return typeof(ushort);
                    case OpType.Ldind_i4: return typeof(int);
                    case OpType.Ldind_u4: return typeof(uint);
                    case OpType.Ldind_i8: return typeof(long);
                    case OpType.Ldind_i: return nativeInt;
                    case OpType.Ldind_r4: return typeof(float);
                    case OpType.Ldind_r8: return typeof(double);
                    default: return typeof(object);
                }
            }
            #endregion
        }
    }
}
