using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

namespace CIL
{
    public class ILReader : IEnumerator<Instruction>
    {
        Module module;
        IList<LocalVariableInfo> locals;
        ParameterInfo[] args;
        byte[] code;
        int i;

        public ILReader(Module module, MethodBody body, ParameterInfo[] args)
        {
            this.module = module;
            this.locals = body.LocalVariables;
            this.args = args;
            this.code = body.GetILAsByteArray();
        }

        public Instruction Current { get; private set; }

        public LocalVariableInfo Local(int slot)
        {
            return locals[slot];
        }

        public ParameterInfo Parameter(int slot)
        {
            return args[slot];
        }

        public struct Label
        {
            internal int pos;
        }

        public Label Mark()
        {
            return new Label { pos = i };
        }

        public void Seek(Label mark)
        {
            i = mark.pos;
        }

        public bool MoveNext()
        {
            if (i >= code.Length) return false;
            var op = IL.GetOpCode(code, ref i);
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
            Current = new Instruction(module, op, arg);
            return true;
        }

        object System.Collections.IEnumerator.Current
        {
            get { return this.Current; }
        }

        public void Dispose()
        {
        }

        public void Reset()
        {
            i = 0;
        }
    }
}
