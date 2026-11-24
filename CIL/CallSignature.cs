using System;
using System.Collections.Generic;
using System.Reflection;

namespace CIL
{
    /// <summary>
    /// Decodes an ECMA-335 StandAloneMethodSig blob (Partition II §23.2.3) used by the <c>calli</c> instruction.
    /// </summary>
    public struct CallSignature
    {
        readonly byte[] signature;

        /// <summary>The calling convention encoded in the signature header.</summary>
        public readonly CallingConventions CallingConvention;

        /// <summary>Whether the signature specifies an explicit <c>this</c> parameter.</summary>
        public readonly bool ExplicitThis;

        /// <summary>Whether the signature specifies an instance <c>this</c> parameter.</summary>
        public bool HasThis => (CallingConvention & CallingConventions.HasThis) != 0;

        /// <summary>The return type of the call site (<see cref="void"/> when returning nothing).</summary>
        public readonly Type ReturnType;

        /// <summary>The explicit parameter types of the call site (excluding implicit <c>this</c>).</summary>
        public readonly Type[] ParameterTypes;

        /// <summary>The index of the vararg sentinel in <see cref="ParameterTypes"/>, or -1 if none.</summary>
        public readonly int VarArgSentinel;

        /// <summary>The raw signature blob bytes.</summary>
        public byte[] RawBytes => signature;

        /// <summary>Decode a standalone method signature without a metadata module (named types resolve to <see cref="object"/>).</summary>
        public CallSignature(byte[] signature)
            : this(signature, null, null, null)
        {
        }

        /// <summary>Decode a standalone method signature using the given module and generic type contexts.</summary>
        public CallSignature(byte[] signature, Module module, Type[] typeContext, Type[] methodContext)
        {
            if (signature == null) throw new ArgumentNullException(nameof(signature));
            this.signature = signature;
            int pos = 0;
            byte header = signature[pos++];

            CallingConventions cc = (CallingConventions)0;
            byte kind = (byte)(header & 0x0F);
            if (kind == 0x05)
                cc |= CallingConventions.VarArgs;
            else
                cc |= CallingConventions.Standard;

            if ((header & 0x20) != 0) cc |= CallingConventions.HasThis;
            if ((header & 0x40) != 0) cc |= CallingConventions.ExplicitThis;
            CallingConvention = cc;
            ExplicitThis = (header & 0x40) != 0;

            // Generic arity if GENERIC (0x10) flag is set
            if ((header & 0x10) != 0)
                ReadCompressedUInt(signature, ref pos);

            int paramCount = (int)ReadCompressedUInt(signature, ref pos);
            ReturnType = ReadRetType(signature, ref pos, module, typeContext, methodContext);

            int sentinel = -1;
            var paramTypes = paramCount == 0 ? Array.Empty<Type>() : new Type[paramCount];
            for (int i = 0; i < paramCount; ++i)
            {
                if (pos < signature.Length && signature[pos] == 0x41) // ELEMENT_TYPE_SENTINEL
                {
                    sentinel = i;
                    ++pos;
                }
                paramTypes[i] = ReadParamType(signature, ref pos, module, typeContext, methodContext);
            }
            ParameterTypes = paramTypes;
            VarArgSentinel = sentinel;
        }

        static uint ReadCompressedUInt(byte[] sig, ref int pos)
        {
            byte b0 = sig[pos++];
            if ((b0 & 0x80) == 0)
                return b0;
            byte b1 = sig[pos++];
            if ((b0 & 0xC0) == 0x80)
                return (uint)(((b0 & 0x3F) << 8) | b1);
            byte b2 = sig[pos++];
            byte b3 = sig[pos++];
            return (uint)(((b0 & 0x1F) << 24) | (b1 << 16) | (b2 << 8) | b3);
        }

        static void SkipCustomMods(byte[] sig, ref int pos)
        {
            while (pos < sig.Length)
            {
                byte b = sig[pos];
                if (b == 0x1F || b == 0x20) // CMOD_REQD, CMOD_OPT
                {
                    ++pos;
                    ReadCompressedUInt(sig, ref pos); // TypeDefOrRefOrSpecEncoded
                }
                else if (b == 0x45) // PINNED
                {
                    ++pos;
                }
                else
                {
                    break;
                }
            }
        }

        static Type ReadRetType(byte[] sig, ref int pos, Module module, Type[] typeCtx, Type[] methodCtx)
        {
            SkipCustomMods(sig, ref pos);
            byte b = sig[pos];
            if (b == 0x01) { ++pos; return typeof(void); }
            if (b == 0x16) { ++pos; return typeof(TypedReference); }
            if (b == 0x10) { ++pos; return ReadType(sig, ref pos, module, typeCtx, methodCtx).MakeByRefType(); }
            return ReadType(sig, ref pos, module, typeCtx, methodCtx);
        }

        static Type ReadParamType(byte[] sig, ref int pos, Module module, Type[] typeCtx, Type[] methodCtx)
        {
            SkipCustomMods(sig, ref pos);
            byte b = sig[pos];
            if (b == 0x16) { ++pos; return typeof(TypedReference); }
            if (b == 0x10) { ++pos; return ReadType(sig, ref pos, module, typeCtx, methodCtx).MakeByRefType(); }
            return ReadType(sig, ref pos, module, typeCtx, methodCtx);
        }

        static Type ResolveTypeDefOrRef(uint encoded, Module module, Type[] typeCtx, Type[] methodCtx)
        {
            if (module == null) return typeof(object);
            int table;
            switch (encoded & 0x03)
            {
                case 0: table = 0x02000000; break; // TypeDef
                case 1: table = 0x01000000; break; // TypeRef
                case 2: table = 0x1B000000; break; // TypeSpec
                default: return typeof(object);
            }
            int token = table | (int)(encoded >> 2);
            return module.ResolveType(token, typeCtx, methodCtx);
        }

        static Type ReadType(byte[] sig, ref int pos, Module module, Type[] typeCtx, Type[] methodCtx)
        {
            SkipCustomMods(sig, ref pos);
            byte elem = sig[pos++];
            switch (elem)
            {
                case 0x01: return typeof(void);
                case 0x02: return typeof(bool);
                case 0x03: return typeof(char);
                case 0x04: return typeof(sbyte);
                case 0x05: return typeof(byte);
                case 0x06: return typeof(short);
                case 0x07: return typeof(ushort);
                case 0x08: return typeof(int);
                case 0x09: return typeof(uint);
                case 0x0A: return typeof(long);
                case 0x0B: return typeof(ulong);
                case 0x0C: return typeof(float);
                case 0x0D: return typeof(double);
                case 0x0E: return typeof(string);
                case 0x0F: // PTR
                {
                    SkipCustomMods(sig, ref pos);
                    if (sig[pos] == 0x01) { ++pos; return typeof(void).MakePointerType(); }
                    return ReadType(sig, ref pos, module, typeCtx, methodCtx).MakePointerType();
                }
                case 0x10: // BYREF
                    return ReadType(sig, ref pos, module, typeCtx, methodCtx).MakeByRefType();
                case 0x11: // VALUETYPE
                case 0x12: // CLASS
                    return ResolveTypeDefOrRef(ReadCompressedUInt(sig, ref pos), module, typeCtx, methodCtx);
                case 0x13: // VAR (class generic param)
                {
                    int idx = (int)ReadCompressedUInt(sig, ref pos);
                    return typeCtx != null && idx < typeCtx.Length ? typeCtx[idx] : typeof(object);
                }
                case 0x14: // ARRAY (multi-dimensional)
                {
                    var elemType = ReadType(sig, ref pos, module, typeCtx, methodCtx);
                    int rank = (int)ReadCompressedUInt(sig, ref pos);
                    int numSizes = (int)ReadCompressedUInt(sig, ref pos);
                    for (int i = 0; i < numSizes; ++i) ReadCompressedUInt(sig, ref pos);
                    int numLoBounds = (int)ReadCompressedUInt(sig, ref pos);
                    for (int i = 0; i < numLoBounds; ++i) ReadCompressedUInt(sig, ref pos);
                    return rank == 1 ? elemType.MakeArrayType() : elemType.MakeArrayType(rank);
                }
                case 0x15: // GENERICINST
                {
                    byte kind = sig[pos++]; // CLASS or VALUETYPE
                    var def = ResolveTypeDefOrRef(ReadCompressedUInt(sig, ref pos), module, typeCtx, methodCtx);
                    int argCount = (int)ReadCompressedUInt(sig, ref pos);
                    var gargs = new Type[argCount];
                    for (int i = 0; i < argCount; ++i)
                        gargs[i] = ReadType(sig, ref pos, module, typeCtx, methodCtx);
                    return def.IsGenericTypeDefinition ? def.MakeGenericType(gargs) : def;
                }
                case 0x16: return typeof(TypedReference);
                case 0x18: return typeof(IntPtr);
                case 0x19: return typeof(UIntPtr);
                case 0x1B: // FNPTR (skip nested method signature)
                {
                    SkipNestedMethodSig(sig, ref pos, module, typeCtx, methodCtx);
                    return typeof(IntPtr);
                }
                case 0x1C: return typeof(object);
                case 0x1D: // SZARRAY
                {
                    SkipCustomMods(sig, ref pos);
                    return ReadType(sig, ref pos, module, typeCtx, methodCtx).MakeArrayType();
                }
                case 0x1E: // MVAR (method generic param)
                {
                    int idx = (int)ReadCompressedUInt(sig, ref pos);
                    return methodCtx != null && idx < methodCtx.Length ? methodCtx[idx] : typeof(object);
                }
                default:
                    throw new NotSupportedException($"Unsupported signature element type: 0x{elem:X2}.");
            }
        }

        static void SkipNestedMethodSig(byte[] sig, ref int pos, Module module, Type[] typeCtx, Type[] methodCtx)
        {
            byte header = sig[pos++];
            if ((header & 0x10) != 0) ReadCompressedUInt(sig, ref pos);
            int pCount = (int)ReadCompressedUInt(sig, ref pos);
            ReadRetType(sig, ref pos, module, typeCtx, methodCtx);
            for (int i = 0; i < pCount; ++i)
            {
                if (pos < sig.Length && sig[pos] == 0x41) ++pos;
                ReadParamType(sig, ref pos, module, typeCtx, methodCtx);
            }
        }
    }
}
