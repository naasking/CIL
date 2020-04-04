using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
            internal int pos;
            public Label(int pos)
            {
                this.pos = pos;
            }
            public override bool Equals(object obj)
            {
                return obj is Label && Equals((Label)obj);
            }
            public override int GetHashCode()
            {
                return pos;
            }
            public bool Equals(Label other)
            {
                return this == other;
            }
            public int CompareTo(Label other)
            {
                return pos.CompareTo(other.pos);
            }
            public override string ToString()
            {
                return "IL_" + pos.ToString("X4");
            }
            /// <summary>
            /// Compare the positions in the bytecode stream.
            /// </summary>
            /// <param name="left"></param>
            /// <param name="right"></param>
            /// <returns></returns>
            public static int operator -(Label left, Label right)
            {
                return left.pos - right.pos;
            }
            public static bool operator <(Label left, Label right)
            {
                return left.pos < right.pos;
            }
            public static bool operator >(Label left, Label right)
            {
                return left.pos > right.pos;
            }
            public static bool operator <=(Label left, Label right)
            {
                return left.pos <= right.pos;
            }
            public static bool operator >=(Label left, Label right)
            {
                return left.pos >= right.pos;
            }
            public static bool operator ==(Label left, Label right)
            {
                return left.pos == right.pos;
            }
            public static bool operator !=(Label left, Label right)
            {
                return left.pos != right.pos;
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
        public static IEnumerable<Instruction> GetInstructions(this MethodBase code) =>
            Decode(code.GetILReader());

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
            var il = code.GetILReader();
            var eval = new Stack<T>();
            var _this = code.IsStatic ? Enumerable.Empty<T>() : new[] { decompiler.Param("this", code.DeclaringType) };
            args = _this.Concat(code.GetParameters().Select(decompiler.Param)).ToArray();
            Process(il, decompiler, eval, new Dictionary<Label, T>(), args, il.Locals.Select(decompiler.Local).ToArray());
            return eval.Count > 1 ? decompiler.Block(eval.Reverse()) : eval.Pop();
        }

        static List<Instruction> Decode(ILReader il)
        {
            var bc = new List<Instruction>();
            while (il.MoveNext())
            {
                bc.Add(il.Current);
                switch (il.Current.OpCode.Type())
                {
                    case OpType.Brfalse:
                    case OpType.Brfalse_s:
                    case OpType.Brtrue:
                    case OpType.Brtrue_s:
                    case OpType.Br:
                    case OpType.Br_s:
                        var target = il.Current.Operand.Label;
                        if (target < il.Current.Label)
                        {
                            //Because instructions may take more than 1 byte, label.pos in IL is always >=
                            //than corresponding index of instruction in bc, so we can't use target.pos to 
                            //find the instruction being jumped to, so we search for it. Might be a good way
                            //to guess a closer starting index for the search.
                            //FIXME: not clear whether this heuristic for a closer starting index will
                            //hold in all cases.
                            var i = FindInstr(bc, (int)Math.Floor(target.pos * (double)bc.Count / il.Current.Label.pos), target);
                            //var i = FindInstr(bc, 0, target);
                            bc[i] = bc[i].AddLoop(il.Current.Label);
                        }
                        break;
                }
            }
            return bc;
        }

        static int FindInstr(List<Instruction> il, int i, IL.Label label)
        {
            while (il[i].Label != label)
                ++i;
            return i;
        }

        static void Process<T>(ILReader il, IExpression<T> exp, Stack<T> eval, Dictionary<Label, T> env, T[] args, T[] locals)
        {
            // the simplest way to add branch sharing, but less efficient
            //if (env.TryGetValue(il.Mark(), out var value))
            //{
            //    eval.Push(value);
            //    return;
            //}
            var tailcall = false;
            while (il.MoveNext())
            {
                var x = il.Current;//.Simplify();
                T rhs;
                Type type;
                //int index;
                switch (x.OpCode.Type())
                {
                    //--------------- arithmetic instructions --------------
                    case OpType.Add_ovf:
                    case OpType.Add_ovf_un:
                        rhs = eval.Pop();
                        eval.Push(exp.AddOverflow(eval.Pop(), rhs));
                        break;
                    case OpType.Add:
                        rhs = eval.Pop();
                        eval.Push(exp.Add(eval.Pop(), rhs));
                        break;
                    case OpType.Sub:
                        rhs = eval.Pop();
                        eval.Push(exp.Subtract(eval.Pop(), rhs));
                        break;
                    case OpType.Sub_ovf:
                    case OpType.Sub_ovf_un:
                        rhs = eval.Pop();
                        eval.Push(exp.SubtractOverflow(eval.Pop(), rhs));
                        break;
                    case OpType.Mul:
                        rhs = eval.Pop();
                        eval.Push(exp.Multiply(eval.Pop(), rhs));
                        break;
                    case OpType.Mul_ovf:
                    case OpType.Mul_ovf_un:
                        rhs = eval.Pop();
                        eval.Push(exp.MultiplyOverflow(eval.Pop(), rhs));
                        break;
                    case OpType.Div:
                    case OpType.Div_un:
                        rhs = eval.Pop();
                        eval.Push(exp.Divide(eval.Pop(), rhs));
                        break;
                    case OpType.Neg:
                        eval.Push(exp.Negate(eval.Pop()));
                        break;
                    case OpType.Rem:
                    case OpType.Rem_un:
                        rhs = eval.Pop();
                        eval.Push(exp.Modulo(eval.Pop(), rhs));
                        break;

                    //------------ logical/bitwise operations --------------
                    case OpType.And:
                        rhs = eval.Pop();
                        eval.Push(exp.And(eval.Pop(), rhs));
                        break;
                    case OpType.Or:
                        rhs = eval.Pop();
                        eval.Push(exp.Or(eval.Pop(), rhs));
                        break;
                    case OpType.Not:
                        eval.Push(exp.Not(eval.Pop()));
                        break;
                    case OpType.Box:
                        eval.Push(exp.Box(eval.Pop()));
                        break;
                    case OpType.Shl:
                        rhs = eval.Pop();
                        eval.Push(exp.LeftShift(eval.Pop(), rhs));
                        break;
                    case OpType.Shr:
                    case OpType.Shr_un:
                        rhs = eval.Pop();
                        eval.Push(exp.RightShift(eval.Pop(), rhs));
                        break;

                    //----------- Branching instructions ---------
                    case OpType.Br:
                    case OpType.Br_s:
                        //FIXME: process in a nested call?
                        il.Seek(x.Operand.Label); // seek to branch label
                        //eval.Push(exp.Goto(eval.Pop()));
                        break;
                    case OpType.Beq:
                    case OpType.Beq_s:
                        rhs = eval.Pop();
                        eval.Push(exp.Equal(eval.Pop(), rhs));
                        goto case OpType.Brtrue;
                    case OpType.Bge:
                    case OpType.Bge_un:
                    case OpType.Bge_un_s:
                        rhs = eval.Pop();
                        eval.Push(exp.GreaterThanOrEqual(eval.Pop(), rhs));
                        goto case OpType.Brtrue;
                    case OpType.Ble:
                    case OpType.Ble_un:
                    case OpType.Ble_un_s:
                        rhs = eval.Pop();
                        eval.Push(exp.LessThanOrEqual(eval.Pop(), rhs));
                        goto case OpType.Brtrue;
                    case OpType.Blt:
                    case OpType.Blt_un:
                    case OpType.Blt_un_s:
                        rhs = eval.Pop();
                        eval.Push(exp.LessThan(eval.Pop(), rhs));
                        goto case OpType.Brtrue;
                    case OpType.Bne_un:
                    case OpType.Bne_un_s:
                        rhs = eval.Pop();
                        eval.Push(exp.NotEqual(eval.Pop(), rhs));
                        goto case OpType.Brtrue;
                    case OpType.Brfalse:
                    case OpType.Brfalse_s:
                        rhs = eval.Pop();
                        type = exp.TypeOf(rhs);
                        eval.Push(exp.Equal(rhs, NullOrZero(exp, type)));
                        goto case OpType.Brtrue;
                    case OpType.Brtrue:
                    case OpType.Brtrue_s:
                        // recursively process if-then-else, but only if the targets haven't
                        // already been visited
                        var cond = eval.Pop();
                        var elseStart = il.Mark();                  // save current position
                        if (!il.MoveNext())
                            throw new InvalidOperationException("Expected an instruction after branch!");
                        var thenStart = x.Operand.Label;
                        if (!env.TryGetValue(elseStart, out var _else))
                        {
                            //il.Seek(elseStart);                    // seek to _else when branch condition false
                            var tmp = new Stack<T>(eval);
                            Process(il, exp, tmp, env, args, locals);
                            // pop until tmp element matches an element in eval, then create block expression
                            var block = new Stack<T>();
                            while (!eval.Contains(tmp.Peek()))
                                block.Push(tmp.Pop());
                            // extract _then expression
                            _else = env[thenStart] = block.Count > 1 ? exp.Block(block) : block.Peek();   // extract _else expression
                        }
                        if (!env.TryGetValue(thenStart, out var _then))
                        {
                            il.Seek(thenStart);                    // seek to _then when branch condition true
                            var tmp = new Stack<T>(eval);
                            Process(il, exp, tmp, env, args, locals);
                            // pop until tmp element matches an element in eval, then create block expression
                            var block = new Stack<T>();
                            while (!eval.Contains(tmp.Peek()))
                                block.Push(tmp.Pop());
                            // extract _then expression
                            _then = env[thenStart] = block.Count > 1 ? exp.Block(block) : block.Peek();
                        }
                        eval.Push(exp.If(cond, _then, _else));
                        break;
                    case OpType.Switch:
                        var val = eval.Pop();
                        if (!il.MoveNext())
                            throw new InvalidOperationException("Expected an instruction after switch!");
                        var swt = il.Mark();
                        var cases = x.ResolveBranches().Select(kv =>
                        {
                            if (!env.TryGetValue(kv.Value, out var caseValue))
                            {
                                il.Seek(kv.Value);
                                var tmp = new Stack<T>(eval);
                                Process(il, exp, tmp, env, args, locals);
                                var block = new Stack<T>();
                                while (!eval.Contains(tmp.Peek()))
                                    block.Push(tmp.Pop());
                                caseValue = tmp.Pop();
                            }
                            return new KeyValuePair<object, T>(kv.Key, caseValue);
                        });
                        eval.Push(exp.Switch(val, cases));
                        il.Seek(swt);
                        break;
#if DEBUG
                    //FIXME: extend ILReader to support consulting exception handling blocks:
                    //  method.GetMethodBody().ExceptionHandlingClauses[0].TryOffset/TryLength/HandlerOffset
                    //
                    case OpType.Leave:
                    case OpType.Leave_s:
                        //FIXME: should leave be treated differently from br?
                        //Process(il, exp, eval, args, locals);   // process the .try block
                        il.Seek(x.Operand.Label); // seek to branch label
                        eval.Push(exp.Goto(eval.Pop()));
                        return;
                    case OpType.Endfilter: // return to try context?
                        //eval.Push()
                        return;
                    //eval.Push(exp.EndFilter(eval.Pop()));
                    //break;
                    case OpType.Endfinally:
                        return;
                    //case OpType.Try:
                    //    Process(il, exp, eval, args, locals);
                    //    var leaveTry = eval.Pop();
                    //    if (il.Current.OpCode.Type() != OpType.Leave || il.Current.OpCode.Type() != OpType.Leave_s || !il.MoveNext())
                    //        throw new InvalidOperationException("Try-catch-finally requires .try block to end with a leave instruction.");
                    //    if (!il.MoveNext()) //FIXME: maybe shouldn't call this?
                    //        return;
                    //    var handlers = new Dictionary<Type, T>();
                    //    do
                    //    {
                    //        Process(il, exp, eval, args, locals);
                    //        if (il.Current.OpCode.Type() != OpType.Endfilter)
                    //    } while (il.Current.OpCode.Type() == OpType.Endfilter);
                    //    Process(il, exp, eval, args, locals);
                    //    if (il.Current.OpCode.Type() != OpType.Leave || il.Current.OpCode.Type() != OpType.Leave_s)
                    //        throw new InvalidOperationException("Try-catch-finally requires .try block to end with a leave instruction.");
                    //    while (il.Current.OpCode.Type() == OpType.Endfinally);
                    //    //eval.Push(exp.TryCatchFinally(leaveTry, ));
                    //    break;
#endif

                    //------------ Method call instructions ---------------
#if DEBUG
                    //FIXME: indirect calls and method pointers require decoding method signatures.
                    //See incomplete CallSignature type.
                    case OpType.Ldftn:
                        eval.Push(exp.GetPointer(x.ResolveMethod(), default(T)));
                        break;
                    case OpType.Ldvirtftn:
                        eval.Push(exp.GetPointer(x.ResolveMethod(), eval.Pop()));
                        break;
                    case OpType.Calli:
                        var sig = x.ResolveSignature();
                        //var args = 
                        var entry = eval.Pop();
                        eval.Push(exp.CallIndirect(sig, entry, tailcall, args));
                        tailcall = false;
                        break;
#endif
                    case OpType.Call:
                    case OpType.Callvirt:
                        var method = x.ResolveMethod();
                        var mparams = method.GetParameters();
                        var margs = mparams.Select((a, i) => Cast(eval.Pop(), mparams[i].ParameterType, exp))
                                           .Reverse()
                                           .ToArray();
                        var minstance = method.IsStatic ? default(T) : eval.Pop();
                        var generated = method.GetCustomAttribute<CompilerGeneratedAttribute>();
                        if (generated != null && method.IsSpecialName && /*method.IsHideBySig &&*/ (mparams.Length == 0 && method.Name.StartsWith("get_") || mparams.Length == 1 && method.Name.StartsWith("set_")))
                        {
                            // decode method call into a property access
                            var pname = method.Name.Substring(4);
                            var prop = method.DeclaringType.GetProperty(pname);
                            eval.Push(margs.Length == 0 ? exp.Property(minstance, prop) : exp.Property(minstance, prop, margs));
                        }
                        else
                        {
                            eval.Push(exp.Call(method, tailcall, minstance, margs));
                        }
                        tailcall = false;
                        break;
                    case OpType.Jmp:
                        eval.Push(exp.Jump(x.ResolveMethod()));
                        break;
                    case OpType.Cpblk:
                        rhs = eval.Pop();
                        var src = eval.Pop();
                        eval.Push(exp.BlockCopy(eval.Pop(), src, rhs));
                        break;
                    case OpType.Cpobj:
                        rhs = eval.Pop();
                        eval.Push(exp.StructCopy(eval.Pop(), rhs, x.ResolveType()));
                        break;
                    case OpType.Initblk:
                        rhs = eval.Pop();
                        var value = eval.Pop();
                        eval.Push(exp.BlockInit(eval.Pop(), value, rhs));
                        break;
                    case OpType.Initobj:
                        eval.Push(exp.ObjectInit(eval.Pop(), x.ResolveType()));
                        break;

                    //---------------- Load/store instructions -------------
                    case OpType.Sizeof:
                        eval.Push(exp.SizeOf(x.ResolveType()));
                        break;
                    case OpType.Localloc:
                        eval.Push(exp.LocalAlloc(eval.Pop()));
                        break;
                    case OpType.Ldsflda:
                    case OpType.Ldflda:
                        var afield = x.ResolveField();
                        eval.Push(exp.AddressOf(exp.Field(afield, afield.IsStatic ? default(T) : eval.Pop())));
                        break;
                    case OpType.Ldarga:
                    case OpType.Ldarga_s:
                        eval.Push(exp.AddressOf(args[x.Operand.Int16]));
                        break;
                    case OpType.Ldloca:
                    case OpType.Ldloca_s:
                        eval.Push(exp.AddressOf(locals[x.Operand.Int16]));
                        break;
                    case OpType.Ldarg:
                    case OpType.Ldarg_s:
                        eval.Push(args[x.Operand.Int16]);
                        break;
                    case OpType.Ldarg_0:
                        eval.Push(args[0]);
                        break;
                    case OpType.Ldarg_1:
                        eval.Push(args[1]);
                        break;
                    case OpType.Ldarg_2:
                        eval.Push(args[2]);
                        break;
                    case OpType.Ldarg_3:
                        eval.Push(args[3]);
                        break;
                    case OpType.Ldc_i4_m1:
                        eval.Push(exp.Constant(-1));
                        break;
                    case OpType.Ldc_i4_0:
                        eval.Push(exp.Constant(0));
                        break;
                    case OpType.Ldc_i4_1:
                        eval.Push(exp.Constant(1));
                        break;
                    case OpType.Ldc_i4_2:
                        eval.Push(exp.Constant(2));
                        break;
                    case OpType.Ldc_i4_3:
                        eval.Push(exp.Constant(3));
                        break;
                    case OpType.Ldc_i4_4:
                        eval.Push(exp.Constant(4));
                        break;
                    case OpType.Ldc_i4_5:
                        eval.Push(exp.Constant(5));
                        break;
                    case OpType.Ldc_i4_6:
                        eval.Push(exp.Constant(6));
                        break;
                    case OpType.Ldc_i4_7:
                        eval.Push(exp.Constant(7));
                        break;
                    case OpType.Ldc_i4_8:
                        eval.Push(exp.Constant(8));
                        break;
                    case OpType.Ldc_i4:
                    case OpType.Ldc_i4_s:
                        var i4 = x.Operand.Int32;
                        eval.Push(exp.Constant(i4));
                        break;
                    case OpType.Ldc_i8:
                        var i8 = x.Operand.Int64;
                        eval.Push(exp.Constant(i8));
                        break;
                    case OpType.Ldc_r8:
                        var r8 = x.Operand.Float64;
                        eval.Push(exp.Constant(r8));
                        break;
                    case OpType.Ldc_r4:
                        var r4 = x.Operand.Float32;
                        eval.Push(exp.Constant(r4));
                        break;
                    case OpType.Ldfld:
                    case OpType.Ldsfld:
                        var lfield = x.ResolveField();
                        eval.Push(exp.Field(lfield, lfield.IsStatic ? default(T) : eval.Pop()));
                        return;
                    case OpType.Ldlen:
                        eval.Push(exp.ArrayLength(eval.Pop()));
                        break;
                    case OpType.Ldloc_0:
                        eval.Push(locals[0]);
                        break;
                    case OpType.Ldloc_1:
                        eval.Push(locals[1]);
                        break;
                    case OpType.Ldloc_2:
                        eval.Push(locals[2]);
                        break;
                    case OpType.Ldloc_3:
                        eval.Push(locals[3]);
                        break;
                    case OpType.Ldloc:
                    case OpType.Ldloc_s:
                        eval.Push(locals[x.Operand.Int32]);
                        break;
                    case OpType.Stloc_0:
                        rhs = eval.Pop();
                        type = exp.TypeOf(rhs);
                        if (exp.TypeOf(locals[0]) == typeof(Boolean) && type != typeof(Boolean))
                            rhs = exp.NotEqual(rhs, NullOrZero(exp, type));
                        eval.Push(exp.Assign(locals[0], rhs));
                        break;
                    case OpType.Stloc_1:
                        rhs = eval.Pop();
                        type = exp.TypeOf(rhs);
                        if (exp.TypeOf(locals[1]) == typeof(Boolean) && type != typeof(Boolean))
                            rhs = exp.NotEqual(rhs, NullOrZero(exp, type));
                        eval.Push(exp.Assign(locals[1], rhs));
                        break;
                    case OpType.Stloc_2:
                        rhs = eval.Pop();
                        type = exp.TypeOf(rhs);
                        if (exp.TypeOf(locals[2]) == typeof(Boolean) && type != typeof(Boolean))
                            rhs = exp.NotEqual(rhs, NullOrZero(exp, type));
                        eval.Push(exp.Assign(locals[2], rhs));
                        break;
                    case OpType.Stloc_3:
                        rhs = eval.Pop();
                        type = exp.TypeOf(rhs);
                        if (exp.TypeOf(locals[3]) == typeof(Boolean) && type != typeof(Boolean))
                            rhs = exp.NotEqual(rhs, NullOrZero(exp, type));
                        eval.Push(exp.Assign(locals[3], rhs));
                        break;
                    case OpType.Stloc:
                    case OpType.Stloc_s:
                        rhs = eval.Pop();
                        type = exp.TypeOf(rhs);
                        if (exp.TypeOf(locals[x.Operand.Int16]) == typeof(Boolean) && type != typeof(Boolean))
                            rhs = exp.NotEqual(rhs, NullOrZero(exp, type));
                        eval.Push(exp.Assign(locals[x.Operand.Int16], rhs));
                        break;
                    case OpType.Starg:
                    case OpType.Starg_s:
                        eval.Push(exp.Assign(args[x.Operand.Int16], eval.Pop()));
                        break;
                    case OpType.Stobj:
                        rhs = eval.Pop();
                        eval.Push(exp.Assign(eval.Pop(), rhs));
                        break;
                    case OpType.Stsfld:
                    case OpType.Stfld:
                        var sfield = x.ResolveField();
                        rhs = eval.Pop();
                        eval.Push(exp.Assign(exp.Field(sfield, sfield.IsStatic ? default(T) : eval.Pop()), rhs));
                        break;
                    case OpType.Ldnull:
                        eval.Push(exp.Constant<object>(null));
                        break;
                    case OpType.Ldstr:
                        eval.Push(exp.Constant(x.ResolveString()));
                        break;
                    case OpType.Ldtoken:
                        eval.Push(exp.Constant(x.Resolve()));
                        break;
                    case OpType.Nop:
                        break;
                    case OpType.Pop:
                        eval.Pop();
                        break;
                    case OpType.Ret:
                        //Debug.Assert(eval.Count == 1);
                        //if (eval.Count > 1)
                        //{
                        //    var block = eval.Reverse().ToList();
                        //    eval.Clear();
                        //    eval.Push(exp.Block(block));
                        //}
                        eval.Push(exp.Return(eval.Pop()));
                        //break;
                        return;
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
                        rhs = eval.Pop();
                        eval.Push(exp.ArrayGet(eval.Pop(), rhs));
                        break;
                    case OpType.Stelem:
                    case OpType.Stelem_i:
                    case OpType.Stelem_i1:
                    case OpType.Stelem_i2:
                    case OpType.Stelem_i4:
                    case OpType.Stelem_i8:
                    case OpType.Stelem_r4:
                    case OpType.Stelem_r8:
                    case OpType.Stelem_ref:
                        rhs = eval.Pop();
                        var idx = eval.Pop();
                        eval.Push(exp.ArraySet(eval.Pop(), idx, rhs));
                        break;
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
                        eval.Push(exp.AddressGet(eval.Pop()));
                        break;
                    case OpType.Stind_ref:
                    case OpType.Stind_i:
                    case OpType.Stind_i1:
                    case OpType.Stind_i2:
                    case OpType.Stind_i4:
                    case OpType.Stind_i8:
                    case OpType.Stind_r4:
                    case OpType.Stind_r8:
                        rhs = eval.Pop();
                        eval.Push(exp.AddressSet(eval.Pop(), rhs));
                        break;
                    case OpType.Arglist:
                        eval.Push(exp.ArgumentList());
                        break;

                    //-------------- Comparison instructions --------------
                    case OpType.Isinst:
                        eval.Push(exp.TypeAs(eval.Pop(), x.ResolveType()));
                        break;
                    case OpType.Ceq:
                        // a bool is a native int in CIL, and != is a comparison against 0
                        rhs = eval.Pop();
                        var lhs = eval.Pop();
                        var tyleft = exp.TypeOf(lhs);
                        var tyright = exp.TypeOf(rhs);
                        if (tyleft != typeof(bool) || tyleft == tyright)
                        {
                            eval.Push(exp.Equal(lhs, rhs));
                        }
                        else if (tyleft != typeof(bool) && tyright == typeof(bool))
                        {
                            eval.Push(exp.Equal(exp.Cast(lhs, tyright), rhs));
                        }
                        else if (tyright != typeof(bool) && tyleft == typeof(bool))
                        {
                            eval.Push(exp.Equal(lhs, exp.Cast(rhs, tyleft)));
                        }
                        else
                        {
                            throw new InvalidOperationException("Unknown bool comparison!");
                        }
                        break;
                    case OpType.Cgt:
                    case OpType.Cgt_un:
                        rhs = eval.Pop();
                        eval.Push(exp.GreaterThan(eval.Pop(), rhs));
                        break;
                    case OpType.Clt:
                    case OpType.Clt_un:
                        rhs = eval.Pop();
                        eval.Push(exp.LessThan(eval.Pop(), rhs));
                        break;
                    case OpType.Ckfinite:
                        var isInfinity = exp.TypeOf(eval.Peek()) == typeof(float) ? r4IsInfinity : r8IsInfinity;
                        var ethrow = exp.Throw(exp.New(arithError, new[] { exp.Constant("Value is not a finite number.") }));
                        var evalue = eval.Pop();
                        eval.Push(exp.If(exp.Call(isInfinity, false, default(T), new[] { evalue }), ethrow, evalue));
                        break;
                    case OpType.Constrained_:
                        break;

                    //------------ Conversion instructions -------------
                    case OpType.Castclass:
                        eval.Push(exp.Cast(eval.Pop(), x.ResolveType()));
                        break;
                    case OpType.Conv_i:
                        eval.Push(exp.Cast(eval.Pop(), nativeInt));
                        break;
                    case OpType.Conv_i1:
                        eval.Push(exp.Cast(eval.Pop(), typeof(sbyte)));
                        break;
                    case OpType.Conv_i2:
                        eval.Push(exp.Cast(eval.Pop(), typeof(short)));
                        break;
                    case OpType.Conv_i4:
                        eval.Push(exp.Cast(eval.Pop(), typeof(int)));
                        break;
                    case OpType.Conv_i8:
                        eval.Push(exp.Cast(eval.Pop(), typeof(long)));
                        break;
                    case OpType.Conv_ovf_i:
                    case OpType.Conv_ovf_i_un:
                        eval.Push(exp.CastOverflow(eval.Pop(), nativeInt));
                        break;
                    case OpType.Conv_ovf_i1:
                    case OpType.Conv_ovf_i1_un:
                        eval.Push(exp.CastOverflow(eval.Pop(), typeof(sbyte)));
                        break;
                    case OpType.Conv_ovf_i2:
                    case OpType.Conv_ovf_i2_un:
                        eval.Push(exp.CastOverflow(eval.Pop(), typeof(short)));
                        break;
                    case OpType.Conv_ovf_i4:
                    case OpType.Conv_ovf_i4_un:
                        eval.Push(exp.CastOverflow(eval.Pop(), typeof(int)));
                        break;
                    case OpType.Conv_ovf_i8:
                    case OpType.Conv_ovf_i8_un:
                        eval.Push(exp.CastOverflow(eval.Pop(), typeof(long)));
                        break;
                    case OpType.Conv_r4:
                    case OpType.Conv_r_un:
                        eval.Push(exp.Cast(eval.Pop(), typeof(float)));
                        break;
                    case OpType.Conv_r8:
                        eval.Push(exp.Cast(eval.Pop(), typeof(double)));
                        break;
                    case OpType.Conv_u:
                        eval.Push(exp.Cast(eval.Pop(), typeof(UIntPtr)));
                        break;
                    case OpType.Conv_u1:
                        eval.Push(exp.Cast(eval.Pop(), typeof(byte)));
                        break;
                    case OpType.Conv_u2:
                        eval.Push(exp.Cast(eval.Pop(), typeof(ushort)));
                        break;
                    case OpType.Conv_u4:
                        eval.Push(exp.Cast(eval.Pop(), typeof(uint)));
                        break;
                    case OpType.Conv_u8:
                        eval.Push(exp.Cast(eval.Pop(), typeof(ulong)));
                        break;
                    case OpType.Dup:
                        eval.Push(exp.Duplicate(eval.Pop()));
                        break;
                    case OpType.Newarr:
                        var atype = x.ResolveType();
                        eval.Push(exp.Array(atype, new[] { eval.Pop() }));
                        break;
                    case OpType.Newobj:
                        var ctor = (ConstructorInfo)x.ResolveMethod();
                        var cparams = ctor.GetParameters();
                        var cargs = cparams.Select(a => eval.Pop()).Reverse();
                        eval.Push(exp.New(ctor, cargs));
                        break;
                    case OpType.Rethrow:
                        eval.Push(exp.Rethrow());
                        break;
                    case OpType.Tail_:
                        tailcall = true;
                        break;
                    case OpType.Unaligned_:
                        eval.Push(exp.Unaligned(eval.Pop()));
                        break;
                    case OpType.Unbox:
                    case OpType.Unbox_any:
                        eval.Push(exp.Unbox(eval.Pop(), x.ResolveType()));
                        break;
                    //case OpType.Break:
                    //    break;
                    //case OpType.Volatile_:
                    //    break;
                    //case OpType.Refanytype:
                    //case OpType.Refanyval:
                    //case OpType.Mkrefany:
                    //case OpType.Localloc:
                    //case OpType.Readonly_:
                    default:
                        throw new NotSupportedException("Instruction not supported: " + x);
                }
            }
        }

        static T NullOrZero<T>(IExpression<T> exp, Type type)
        {
            return type == typeof(int) || type == typeof(long)
                 ? exp.Constant(0)
                 : exp.Constant(null, type);
        }

        static T Cast<T>(T e, Type type, IExpression<T> exp)
        {
            return exp.TypeOf(e) == type ? e:
                   exp.IsConstant(e)     ? exp.Constant(Convert.ChangeType(exp.Value(e), type)):
                                           exp.Cast(e, type);
        }
    }
}
