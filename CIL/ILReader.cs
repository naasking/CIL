﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

namespace CIL
{
    /// <summary>
    /// Used to read CIL bytecodes.
    /// </summary>
    public class ILReader : IEnumerator<Instruction>
    {
        Module module;
        Type[] methodContext;
        Type[] typeContext;
        byte[] code;
        ExceptionHandlingClauseOptions?[] scopes;
        int i;
        int bound;

        /// <summary>
        /// Construct an instance of a CIL reader.
        /// </summary>
        /// <param name="method">The method to analyze.</param>
        public ILReader(MethodBase method, out int byteCodeLength)
        {
            if (method == null) throw new ArgumentNullException("method");
            this.module = method.Module;
            this.methodContext = method.IsConstructor ? null : method.GetGenericArguments();
            this.typeContext = method.DeclaringType.GenericTypeArguments;
            this.Args = method.GetParameters();
            var body = method.GetMethodBody();
            this.Locals = body?.LocalVariables ?? new List<LocalVariableInfo>(0);
            this.code = body?.GetILAsByteArray() ?? new byte[0];
            byteCodeLength = this.bound = code.Length;
            //method.GetMethodBody().ExceptionHandlingClauses[0].Flags == ExceptionHandlingClauseOptions
            // enum ScopeType { Normal, Try, Catch, Finally }
            //Dictionary<int, ScopeType> ??
        }

        internal Instruction current;

        /// <summary>
        /// The current instruction.
        /// </summary>
        public Instruction Current => current;

        /// <summary>
        /// The method's locals.
        /// </summary>
        public IList<LocalVariableInfo> Locals { get; private set; }

        /// <summary>
        /// The method's arguments.
        /// </summary>
        public IList<ParameterInfo> Args { get; private set; }

        /// <summary>
        /// Generate a label marking a position in the instruction stream.
        /// </summary>
        /// <returns></returns>
        public IL.Label Mark()
        {
            return new IL.Label(i);
        }

        /// <summary>
        /// Move the reader's position to the marked position.
        /// </summary>
        /// <param name="mark"></param>
        public void Seek(IL.Label mark)
        {
            this.i = mark.Offset;
        }

        ///// <summary>
        ///// Move the reader's position to the marked position.
        ///// </summary>
        ///// <param name="mark"></param>
        //public void Seek(IL.Label mark, IL.Label bound)
        //{
        //    if (bound < mark) throw new ArgumentOutOfRangeException("bound", "The IL.Label's upper bound must be greater than or equal to the mark.");
        //    this.i = mark.pos;
        //    this.bound = bound.pos;
        //}

        /// <summary>
        /// Move to the next instruction.
        /// </summary>
        /// <returns>True if successful, false if at the end of the instruction sequence.</returns>
        public bool MoveNext()
        {
            if (i >= code.Length)
                return false;
            var label = i;
            var op = IL.GetOpCode(code, ref i);
            var arg = default(Operand);
            switch (op.OperandType)
            {
                case OperandType.InlineSwitch:
                    var count = BitConverter.ToInt32(code, i);
                    arg = new Operand(new IL.Label(i));
                    i += 4 + 4 * count; // skip 'count' and 32bit branch target list
                    break;
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineString:
                case OperandType.InlineSig:
                case OperandType.InlineField:
                case OperandType.InlineType:
                case OperandType.InlineTok:
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
                case OperandType.InlineBrTarget:
                    arg = new Operand(new IL.Label(BitConverter.ToInt32(code, i) + i + 4));
                    i += 4;
                    break;
                case OperandType.ShortInlineBrTarget:
                    arg = new Operand(new IL.Label((sbyte)code[i] + i + 1));
                    i += 1;
                    break;
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    arg = new Operand(code[i]);
                    i += 1;
                    break;
            }
            current = new Instruction(this, op, arg);
            return true;
        }

        object System.Collections.IEnumerator.Current
        {
            get { return this.Current; }
        }

        /// <summary>
        /// Dispose of the reader.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Reset the reader to the first instruction.
        /// </summary>
        public void Reset()
        {
            i = 0;
        }

        #region Internal operations on instructions
        internal FieldInfo ResolveField(int token)
        {
            return module.ResolveField(token, typeContext, methodContext);
        }

        internal MethodBase ResolveMethod(int token)
        {
            return module.ResolveMethod(token, typeContext, methodContext);
        }

        internal string ResolveString(int token)
        {
            return module.ResolveString(token);
        }

        internal Type ResolveType(int token)
        {
            return module.ResolveType(token, typeContext, methodContext);
        }

        internal MemberInfo ResolveMember(int token)
        {
            return module.ResolveMember(token, typeContext, methodContext);
        }

        internal byte[] ResolveSignature(int token)
        {
            return module.ResolveSignature(token);
        }

        internal IEnumerable<KeyValuePair<int, IL.Label>> ResolveBranches(int pos)
        {
            var count = BitConverter.ToInt32(code, pos);
            pos += 4;
            var basep = pos + 4 * count;
            for (int i = 0; i < count; ++i)
            {
                var offset = BitConverter.ToInt32(code, pos);
                yield return new KeyValuePair<int, IL.Label>(offset, new IL.Label(basep + offset));
                pos += 4;
            }
        }
        #endregion
    }
}
