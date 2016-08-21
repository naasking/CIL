using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;

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

        /// <summary>
        /// Decompile a method using the specified decompiler.
        /// </summary>
        /// <typeparam name="T">The type of decompiled expressions.</typeparam>
        /// <param name="code">The method to decompile.</param>
        /// <param name="decompiler">The CIL expression decompiler.</param>
        /// <returns>An expression representing the code.</returns>
        public static T Decompile<T>(this MethodBase code, IDecompiler<T> decompiler)
        {
            var il = code.GetILReader();
            var eval = new Stack<T>();
            Process(il, decompiler, eval, code.GetParameters().Select(decompiler.Param).ToList(), il.Locals.Select(decompiler.Local).ToList());
            return eval.Pop();
        }

        static void Process<T>(ILReader il, IDecompiler<T> exp, Stack<T> eval, List<T> args, List<T> locals)
        {
            while (!il.MoveNext())
            {
                var x = il.Current.Simplify();
                T rhs;
                switch (x.OpCode.Type())
                {
                    case OpType.Add_ovf:
                    case OpType.Add_ovf_un:
                        rhs = eval.Pop();
                        eval.Push(exp.AddOverflow(eval.Pop(), rhs));
                        break;
                    case OpType.Add:
                        rhs = eval.Pop();
                        eval.Push(exp.Add(eval.Pop(), rhs));
                        break;
                    case OpType.And:
                        rhs = eval.Pop();
                        eval.Push(exp.And(eval.Pop(), rhs));
                        break;
                    case OpType.Box:
                        eval.Push(exp.Box(eval.Pop()));
                        break;
                    //FIXME: add remaining branch instructions
                    case OpType.Brtrue:
                        var cond = eval.Pop();
                        if (!il.MoveNext())
                            throw new InvalidOperationException("Expected an instruction after branch!");
                        var elseStart = il.Mark();// save current position
                        il.Seek(x.Operand.Label); // seek to _then when branch condition true
                        Process(il, exp, eval, args, locals);
                        var _then = eval.Pop();   // extract _then expression
                        il.Seek(elseStart);       // seek to _else when branch condition false
                        Process(il, exp, eval, args, locals);
                        var _else = eval.Pop();   // extract _else expression
                        eval.Push(exp.If(cond, _then, _else));
                        break;
                    case OpType.Switch:
                        var val = eval.Pop();
                        if (!il.MoveNext())
                            throw new InvalidOperationException("Expected an instruction after switch!");
                        var swt = il.Mark();
                        var cases = x.ResolveBranches().Select(lbl =>
                        {
                            il.Seek(lbl);
                            Process(il, exp, eval, args, locals);
                            return eval.Pop();
                        });
                        eval.Push(exp.Switch(val, cases));
                        il.Seek(swt);
                        break;
                    case OpType.Castclass:
                        eval.Push(exp.Cast(eval.Pop(), x.ResolveType()));
                        break;
                    case OpType.Call:
                    case OpType.Callvirt:
                        var method = x.ResolveMethod();
                        var mparams = method.GetParameters();
                        var margs = mparams.Select((a, i) => Cast(eval.Pop(), mparams[i].ParameterType, exp))
                                           .Reverse()
                                           .ToArray();
                        var minstance = method.IsStatic ? default(T) : eval.Pop();
                        eval.Push(x.OpCode.Type() == OpType.Call ? exp.Call(method, minstance, margs) : exp.CallVirt(method, minstance, margs));
                        break;
                    case OpType.Isinst:
                        eval.Push(exp.TypeAs(eval.Pop(), x.ResolveType()));
                        break;
                    case OpType.Ldarg:
                        eval.Push(args[x.Operand.Int16]);
                        break;
                    case OpType.Ldc_i4:
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
                        var field = x.ResolveField();
                        eval.Push(exp.Field(field, field.IsStatic ? default(T) : eval.Pop()));
                        return;
                    case OpType.Ldlen:
                        eval.Push(exp.ArrayLength(eval.Pop()));
                        break;
                    case OpType.Ldloc:
                        eval.Push(locals[x.Operand.Int32]);
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
                        Debug.Assert(eval.Count == 1);
                        eval.Push(exp.Return(eval.Pop()));
                        break;
                }
            }
        }

        static T Cast<T>(T e, Type type, IDecompiler<T> exp)
        {
            return exp.Typeof(e) == type ? e:
                   exp.IsConstant(e)     ? exp.Constant(Convert.ChangeType(exp.ValueOf(e), type)):
                                           exp.Cast(e, type);
        }
    }
}
