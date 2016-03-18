using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace CIL
{
    [StructLayout(LayoutKind.Explicit)]
    public struct Operand
    {
        [FieldOffset(0)]
        public readonly double Float64;
        [FieldOffset(4)]
        public readonly double Float32;
        [FieldOffset(0)]
        public readonly long Int64;
        [FieldOffset(0)]
        public readonly ulong UInt64;
        [FieldOffset(4)]
        public readonly int Int32;
        [FieldOffset(4)]
        public readonly uint UInt32;
        [FieldOffset(6)]
        public readonly short Int16;
        [FieldOffset(6)]
        public readonly ushort UInt16;
        [FieldOffset(7)]
        public readonly sbyte Int8;
        [FieldOffset(7)]
        public readonly byte UInt8;
        [FieldOffset(6)]
        public readonly char Char;

        // use Module.Resolve* to extract the string, fields, methods, or other values.
        [FieldOffset(4)]
        public readonly int MetadataToken;

        public Operand(int x)
            : this()
        {
            this.Int32 = x;
        }
        public Operand(uint x)
            : this()
        {
            this.UInt32 = x;
        }
        public Operand(short x)
            : this()
        {
            this.Int16 = x;
        }
        public Operand(ushort x)
            : this()
        {
            this.UInt16 = x;
        }
        public Operand(sbyte x)
            : this()
        {
            this.Int8 = x;
        }
        public Operand(byte x)
            : this()
        {
            this.UInt8 = x;
        }
        public Operand(float x)
            : this()
        {
            this.Float32 = x;
        }
        public Operand(double x)
            : this()
        {
            this.Float64 = x;
        }
        public Operand(char x)
            : this()
        {
            this.Char = x;
        }
        public static implicit operator Operand(int x)
        {
            return new Operand(x);
        }
        public static implicit operator Operand(double x)
        {
            return new Operand(x);
        }
        public static implicit operator Operand(float x)
        {
            return new Operand(x);
        }
        public static implicit operator Operand(long x)
        {
            return new Operand(x);
        }
    }

    public struct Instruction
    {
        public readonly OpCode OpCode;
        public readonly Operand Operand;
        public readonly Module Module;

        public Instruction(Module module, OpCode op, Operand arg)
        {
            this.Module = module;
            this.OpCode = op;
            this.Operand = arg;
        }

        public Instruction(Module module, OpCode op)
            : this(module, op, default(Operand))
        {
        }

        public Module GetModule()
        {
            //return Assembly.GetCallingAssembly().ManifestModule;
            return Module;
        }

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

        public FieldInfo ResolveField()
        {
            if (OpCode.OperandType != OperandType.InlineField)
                throw new InvalidOperationException("Instruction does not reference a field.");
            return GetModule().ResolveField(Operand.MetadataToken);
        }

        public MethodBase ResolveMethod()
        {
            if (OpCode.OperandType != OperandType.InlineMethod)
                throw new InvalidOperationException("Instruction does not reference a method.");
            return GetModule().ResolveMethod(Operand.MetadataToken);
        }

        public string ResolveString()
        {
            if (OpCode.OperandType != OperandType.InlineString)
                throw new InvalidOperationException("Instruction does not reference a string.");
            return GetModule().ResolveString(Operand.MetadataToken);
        }

        public Type ResolveType()
        {
            if (OpCode.OperandType != OperandType.InlineType || OpCode.OperandType != OperandType.InlineTok)
                throw new InvalidOperationException("Instruction does not reference a type or token.");
            return GetModule().ResolveType(Operand.MetadataToken);
        }

        public override string ToString()
        {
            return OpCode.Name + " " + Resolve();
        }
    }
}
