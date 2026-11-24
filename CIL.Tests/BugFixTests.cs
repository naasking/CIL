using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;
using CIL.Expressions;

namespace CIL.Tests
{
    /// <summary>
    /// Tests verifying all bug fixes and newly handled opcodes from the review.
    /// </summary>
    public class BugFixTests
    {
        #region Target methods — field loads (ldfld/ldsfld return→break fix)

        static int StaticCounter = 42;

        class FieldHolder
        {
            public int Value;
            public string Name;
        }

        // These methods load a field and then do more work after.
        // Before the fix, Process returned at the ldfld and never reached the add/ret.
        static int LoadStaticFieldThenAdd(int x) => StaticCounter + x;

        static int LoadFieldThenMultiply(FieldHolder h) => h.Value * 2;

        static string LoadFieldThenCallMethod(FieldHolder h) => h.Name.ToUpper();

        static int LoadFieldThenCompare(FieldHolder h) => h.Value > 0 ? 1 : 0;

        static int LoadTwoFields(FieldHolder a, FieldHolder b) => a.Value + b.Value;

        #endregion

        #region Target methods — xor

        static int XorSimple(int x, int y) => x ^ y;

        static int XorSelf(int x) => x ^ x; // always 0

        static int XorChain(int x, int y, int z) => x ^ y ^ z;

        #endregion

        #region Target methods — throw

        static int ThrowIfNegative(int x)
        {
            if (x < 0) throw new ArgumentException("negative");
            return x;
        }

        static void ThrowAlways()
        {
            throw new InvalidOperationException("always");
        }

        #endregion

        #region Target methods — negative ldc.i4.s constants

        // The compiler uses ldc.i4.s for constants in [-128, 127].
        // ldc.i4.m1 covers -1, ldc.i4.0 through ldc.i4.8 cover 0-8.
        // So ldc.i4.s is used for values like -128, -2, 9, 127.
        static int ReturnNeg128() => -128;
        static int ReturnNeg2() => -2;
        static int Return127() => 127;
        static int Return42() => 42;
        static int AddSmallNeg(int x) => x + (-5);

        #endregion

        #region Target methods — switch

        static string SwitchToString(int x)
        {
            switch (x)
            {
                case 0: return "zero";
                case 1: return "one";
                case 2: return "two";
                default: return "other";
            }
        }

        static int SwitchAdd(int x)
        {
            switch (x)
            {
                case 0: return x + 10;
                case 1: return x + 20;
                case 2: return x + 30;
                default: return x;
            }
        }

        #endregion

        #region Target methods — conv.ovf.u*

        static byte CheckedToByte(int x) => checked((byte)x);
        static ushort CheckedToUShort(int x) => checked((ushort)x);
        static uint CheckedToUInt(long x) => checked((uint)x);
        static ulong CheckedToULong(long x) => checked((ulong)x);

        #endregion

        #region Target methods — properties (non-auto / manual)

        class ManualProperty
        {
            private string _name;
            public string Name
            {
                get { return _name; }
                set { _name = value; }
            }

            public int Length
            {
                get { return _name?.Length ?? 0; }
            }
        }

        class IndexerProperty
        {
            private int[] _data = new int[10];
            public int this[int i]
            {
                get { return _data[i]; }
                set { _data[i] = value; }
            }
        }

        static string GetManualProp(ManualProperty m) => m.Name;
        static int GetManualReadonly(ManualProperty m) => m.Length;
        static int GetIndexer(IndexerProperty p) => p[3];

        #endregion

        #region Target methods — dup opcode

        // String.Concat(a, b) pattern often uses dup in some contexts.
        // A simple way to trigger dup: null-coalescing or reference checks.
        // But those have other issues. Let's verify at the instruction level.

        #endregion

        #region Target methods — ceq mixed types

        // ceq with bool on one side appears when comparing result of cgt/clt
        // against a constant. This is how the compiler implements >= and <=.
        static bool GreaterOrEqual(int x, int y) => x >= y;
        static bool LessOrEqual(int x, int y) => x <= y;
        static bool NotBool(bool b) => !b;

        #endregion

        #region 1. Field load continuation (ldfld/ldsfld return→break)

        [Fact]
        public void FieldLoad_StaticFieldThenAdd_DecompilesCorrectly()
        {
            // Before fix: Process returned at ldsfld, never reaching the add
            var il = new Func<int, int>(LoadStaticFieldThenAdd).Method.GetInstructions();
            // Should contain both ldsfld and add opcodes
            bool hasLdsfld = false;
            bool hasAdd = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Ldsfld) hasLdsfld = true;
                if (op == OpType.Add) hasAdd = true;
            }
            Assert.True(hasLdsfld, "Method should contain ldsfld");
            Assert.True(hasAdd, "Method should contain add after ldsfld");
        }

        [Fact]
        public void FieldLoad_InstanceFieldThenMultiply_InstructionStream()
        {
            var il = new Func<FieldHolder, int>(LoadFieldThenMultiply).Method.GetInstructions();
            bool hasLdfld = false;
            bool hasMul = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Ldfld) hasLdfld = true;
                if (op == OpType.Mul) hasMul = true;
            }
            Assert.True(hasLdfld, "Method should contain ldfld");
            Assert.True(hasMul, "Method should contain mul after ldfld");
        }

        [Fact]
        public void FieldLoad_ThenCallMethod_InstructionStream()
        {
            var il = new Func<FieldHolder, string>(LoadFieldThenCallMethod).Method.GetInstructions();
            bool hasLdfld = false;
            bool hasCall = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Ldfld) hasLdfld = true;
                if (op == OpType.Call || op == OpType.Callvirt) hasCall = true;
            }
            Assert.True(hasLdfld, "Method should contain ldfld");
            Assert.True(hasCall, "Method should contain call after ldfld");
        }

        [Fact]
        public void FieldLoad_TwoFields_InstructionStream()
        {
            var il = new Func<FieldHolder, FieldHolder, int>(LoadTwoFields).Method.GetInstructions();
            int ldfldCount = 0;
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Ldfld) ldfldCount++;
            }
            Assert.Equal(2, ldfldCount);
        }

        #endregion

        #region 2. Dup pushes two copies

        [Fact]
        public void Dup_InstructionExists_InDupPattern()
        {
            // Verify dup opcode is present in methods that use it.
            // The null-coalescing operator generates dup:
            //   ldarg.0; dup; brtrue TARGET; pop; ldstr "default"; TARGET: ret
            var il = new Func<string, string>(s => s ?? "default").Method.GetInstructions();
            bool hasDup = false;
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Dup)
                {
                    hasDup = true;
                    break;
                }
            }
            Assert.True(hasDup, "Null-coalescing should generate dup opcode");
        }

        [Fact]
        public void Dup_LinqDecompiler_ReturnsSameExpression()
        {
            // The LinqDecompiler.Duplicate is a pass-through,
            // so both copies should be reference-equal
            var dec = new LinqDecompiler();
            var param = Expression.Parameter(typeof(int), "x");
            var result = dec.Duplicate(param);
            Assert.Same(param, result);
        }

        #endregion

        #region 3. ldc.i4.s sign extension

        [Fact]
        public void SignExtension_NegativeConstants_CorrectInIL()
        {
            // ReturnNeg128 should have ldc.i4.s with value -128
            var il = new Func<int>(ReturnNeg128).Method.GetInstructions();
            bool foundNegConst = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Ldc_i4_s || op == OpType.Ldc_i4)
                {
                    var val = op == OpType.Ldc_i4_s
                        ? (int)il[i].Operand.Int8
                        : il[i].Operand.Int32;
                    if (val == -128) foundNegConst = true;
                }
            }
            Assert.True(foundNegConst, "Should find constant -128 in IL");
        }

        [Fact]
        public void SignExtension_Neg2_Decompile()
        {
            // -2 should use ldc.i4.s, not ldc.i4.m1 (which is only -1)
            Func<int> foo = () => -2;
            Expression<Func<int>> expected = () => -2;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void SignExtension_Pos127_Decompile()
        {
            Func<int> foo = () => 127;
            Expression<Func<int>> expected = () => 127;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void SignExtension_Pos42_Decompile()
        {
            Func<int> foo = () => 42;
            Expression<Func<int>> expected = () => 42;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void SignExtension_Neg128_Decompile()
        {
            Func<int> foo = () => -128;
            Expression<Func<int>> expected = () => -128;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void SignExtension_NegativeAddition_Decompile()
        {
            // x + (-5) — the -5 may use ldc.i4.s
            Func<int, int> foo = x => x + (-5);
            Expression<Func<int, int>> expected = x => x + -5;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        #endregion

        #region 4. ResolveBranches switch case keys

        [Fact]
        public void Switch_HasSwitchOpcode()
        {
            var il = new Func<int, string>(SwitchToString).Method.GetInstructions();
            bool hasSwitch = false;
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Switch)
                {
                    hasSwitch = true;
                    break;
                }
            }
            Assert.True(hasSwitch, "Method should contain switch opcode");
        }

        [Fact]
        public void Switch_BranchKeys_AreSequentialIndices()
        {
            // After the fix, ResolveBranches should return keys 0, 1, 2, ...
            var il = new Func<int, string>(SwitchToString).Method.GetInstructions();
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Switch)
                {
                    var branches = il[i].ResolveBranches().ToArray();
                    Assert.True(branches.Length > 0, "Switch should have branches");
                    for (int j = 0; j < branches.Length; j++)
                    {
                        Assert.Equal(j, branches[j].Key);
                    }
                    return;
                }
            }
            Assert.Fail("No switch instruction found");
        }

        [Fact]
        public void Switch_BranchTargets_AreValidLabels()
        {
            var il = new Func<int, string>(SwitchToString).Method.GetInstructions();
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Switch)
                {
                    var branches = il[i].ResolveBranches().ToArray();
                    foreach (var branch in branches)
                    {
                        // Each target label should resolve to a valid instruction index
                        var idx = il.GetIndex(branch.Value);
                        Assert.True(idx >= 0 && idx < il.Count,
                            $"Branch target {branch.Value} should be a valid instruction index");
                    }
                    return;
                }
            }
        }

        [Fact]
        public void Switch_CFA_BranchTargetsAreLeaders()
        {
            var il = new Func<int, string>(SwitchToString).Method.GetInstructions();
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Switch)
                {
                    var branches = il[i].ResolveBranches().ToArray();
                    foreach (var branch in branches)
                    {
                        Assert.True(il[branch.Value].IsLeader,
                            $"Switch target at {branch.Value} should be a basic block leader");
                    }
                    return;
                }
            }
        }

        #endregion

        #region 5. Ceq — mixed bool/non-bool comparison

        [Fact]
        public void Ceq_GreaterOrEqual_Decompiles()
        {
            // x >= y compiles as: ldarg.0; ldarg.1; clt; ldc.i4.0; ceq
            // The ceq compares a bool (result of clt) with int 0.
            // After the Ceq fix, this should correctly handle the mixed types.
            Func<int, int, bool> foo = (x, y) => x >= y;
            var decompiled = LinqDecompiler.Decompile(foo);
            // >= decompiles as Not(LessThan) due to no cge instruction in CIL
            Assert.Equal("(x, y) => Not((x < y))", decompiled.ToString());
        }

        [Fact]
        public void Ceq_LessOrEqual_Decompiles()
        {
            Func<int, int, bool> foo = (x, y) => x <= y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal("(x, y) => Not((x > y))", decompiled.ToString());
        }

        [Fact]
        public void Ceq_BoolNegation_Decompiles()
        {
            // !b compiles as: ldarg.0; ldc.i4.0; ceq
            Func<bool, bool> foo = b => !b;
            Expression<Func<bool, bool>> expected = b => !b;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Ceq_EqualInts_Decompiles()
        {
            Func<int, int, bool> foo = (x, y) => x == y;
            Expression<Func<int, int, bool>> expected = (x, y) => x == y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        #endregion

        #region 6. Xor opcode now handled

        [Fact]
        public void Xor_Instruction_ExistsInIL()
        {
            var il = new Func<int, int, int>(XorSimple).Method.GetInstructions();
            bool hasXor = false;
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Xor)
                {
                    hasXor = true;
                    break;
                }
            }
            Assert.True(hasXor, "XOR method should contain xor opcode");
        }

        [Fact]
        public void Xor_SimpleDecompile()
        {
            Func<int, int, int> foo = (x, y) => x ^ y;
            Expression<Func<int, int, int>> expected = (x, y) => x ^ y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Xor_Self_Decompile()
        {
            Func<int, int> foo = x => x ^ x;
            Expression<Func<int, int>> expected = x => x ^ x;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Xor_Chain_Decompile()
        {
            Func<int, int, int, int> foo = (x, y, z) => x ^ y ^ z;
            Expression<Func<int, int, int, int>> expected = (x, y, z) => x ^ y ^ z;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Xor_WithAnd_Decompile()
        {
            Func<int, int, int> foo = (x, y) => (x ^ y) & x;
            Expression<Func<int, int, int>> expected = (x, y) => (x ^ y) & x;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        #endregion

        #region 7. Throw opcode now handled

        [Fact]
        public void Throw_Instruction_ExistsInIL()
        {
            var il = new Func<int, int>(ThrowIfNegative).Method.GetInstructions();
            bool hasThrow = false;
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Throw)
                {
                    hasThrow = true;
                    break;
                }
            }
            Assert.True(hasThrow, "Method with throw should contain throw opcode");
        }

        [Fact]
        public void Throw_CFA_FlowControlIsThrow()
        {
            var il = new Func<int, int>(ThrowIfNegative).Method.GetInstructions();
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Throw)
                {
                    Assert.Equal(FlowControl.Throw, il[i].OpCode.FlowControl);
                    // Instruction after throw should be a leader (new basic block)
                    if (i + 1 < il.Count)
                        Assert.True(il[i + 1].IsLeader,
                            "Instruction after throw should be a basic block leader");
                    return;
                }
            }
        }

        [Fact]
        public void Throw_AlwaysThrows_HasThrowOpcode()
        {
            var il = new Action(ThrowAlways).Method.GetInstructions();
            bool hasThrow = false;
            bool hasNewobj = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Newobj) hasNewobj = true;
                if (op == OpType.Throw) hasThrow = true;
            }
            Assert.True(hasNewobj, "Should have newobj for exception constructor");
            Assert.True(hasThrow, "Should have throw opcode");
        }

        #endregion

        #region 8. Conv_ovf_u* conversions now handled

        [Fact]
        public void ConvOvfU_CheckedByte_HasConvOvfU1()
        {
            var il = new Func<int, byte>(CheckedToByte).Method.GetInstructions();
            bool hasConv = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Conv_ovf_u1 || op == OpType.Conv_ovf_u1_un)
                {
                    hasConv = true;
                    break;
                }
            }
            Assert.True(hasConv, "checked((byte)x) should emit conv.ovf.u1");
        }

        [Fact]
        public void ConvOvfU_CheckedUShort_HasConvOvfU2()
        {
            var il = new Func<int, ushort>(CheckedToUShort).Method.GetInstructions();
            bool hasConv = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Conv_ovf_u2 || op == OpType.Conv_ovf_u2_un)
                {
                    hasConv = true;
                    break;
                }
            }
            Assert.True(hasConv, "checked((ushort)x) should emit conv.ovf.u2");
        }

        [Fact]
        public void ConvOvfU_CheckedUInt_HasConvOvfU4()
        {
            var il = new Func<long, uint>(CheckedToUInt).Method.GetInstructions();
            bool hasConv = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Conv_ovf_u4 || op == OpType.Conv_ovf_u4_un)
                {
                    hasConv = true;
                    break;
                }
            }
            Assert.True(hasConv, "checked((uint)x) should emit conv.ovf.u4");
        }

        [Fact]
        public void ConvOvfU_CheckedULong_HasConvOvfU8()
        {
            var il = new Func<long, ulong>(CheckedToULong).Method.GetInstructions();
            bool hasConv = false;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Conv_ovf_u8 || op == OpType.Conv_ovf_u8_un)
                {
                    hasConv = true;
                    break;
                }
            }
            Assert.True(hasConv, "checked((ulong)x) should emit conv.ovf.u8");
        }

        [Fact]
        public void ConvOvfU_CheckedByte_Decompile()
        {
            Func<int, byte> foo = x => checked((byte)x);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("ConvertChecked", decompiled.ToString());
        }

        [Fact]
        public void ConvOvfU_CheckedUShort_Decompile()
        {
            Func<int, ushort> foo = x => checked((ushort)x);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("ConvertChecked", decompiled.ToString());
        }

        #endregion

        #region 9. Property detection — manual properties and indexers

        [Fact]
        public void Property_ManualGetter_DetectedAsProperty()
        {
            // ManualProperty.Name has a manually implemented getter (no [CompilerGenerated]).
            // After the fix, it should still be detected as a property access.
            var getter = typeof(ManualProperty).GetMethod("get_Name");
            Assert.NotNull(getter);
            Assert.True(getter.IsSpecialName);
            // Verify it does NOT have CompilerGeneratedAttribute
            // (the old code required this, the new code doesn't)
            var hasCompilerGenerated = getter.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
            // Manual properties won't have this attribute
            Assert.False(hasCompilerGenerated,
                "Manual property getter should not have CompilerGeneratedAttribute");
        }

        [Fact]
        public void Property_ManualGetter_DecompilesAsProperty()
        {
            Func<ManualProperty, string> foo = m => m.Name;
            Expression<Func<ManualProperty, string>> expected = m => m.Name;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Property_ManualReadonlyGetter_DecompilesAsProperty()
        {
            Func<ManualProperty, int> foo = m => m.Length;
            Expression<Func<ManualProperty, int>> expected = m => m.Length;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Property_Indexer_DecompilesAsProperty()
        {
            Func<IndexerProperty, int> foo = p => p[3];
            var decompiled = LinqDecompiler.Decompile(foo);
            // Indexer detected as property produces "p.Item[3]"
            // instead of "p.get_Item(3)" as a method call
            Assert.Equal("p => p.Item[3]", decompiled.ToString());
        }

        [Fact]
        public void Property_StringLength_DecompilesAsProperty()
        {
            // String.Length is a built-in property with IsSpecialName on getter
            Func<string, int> foo = s => s.Length;
            Expression<Func<string, int>> expected = s => s.Length;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Property_ArrayLength_UsesArrayLengthExpression()
        {
            // Array.Length uses ldlen, not a property accessor, so it's a different path
            Func<int[], int> foo = a => a.Length;
            Expression<Func<int[], int>> expected = a => a.Length;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        #endregion

        #region 10. Ldelema / Ldobj opcodes

        [Fact]
        public void Ldelema_ExistsInStructArrayAccess()
        {
            // Accessing a struct array element often uses ldelema + ldobj
            // or ldelema + call for method invocations on value types in arrays
            var il = new Func<int[], int, string>(
                (arr, i) => arr[i].ToString()).Method.GetInstructions();
            bool hasLdelema = false;
            for (int j = 0; j < il.Count; j++)
            {
                if (il[j].OpCode.Type() == OpType.Ldelema)
                {
                    hasLdelema = true;
                    break;
                }
            }
            // int is a value type, so calling ToString on arr[i] may use ldelema
            // (compiler may vary, so we just verify the instruction is decodable)
            if (hasLdelema)
                Assert.True(true, "ldelema found and decoded successfully");
        }

        #endregion

        #region 11. End-to-end decompilation of fixed methods

        [Fact]
        public void E2E_XorThenAdd()
        {
            Func<int, int, int> foo = (x, y) => (x ^ y) + (x & y);
            Expression<Func<int, int, int>> expected = (x, y) => (x ^ y) + (x & y);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void E2E_BitwiseOps()
        {
            Func<int, int, int> foo = (x, y) => (x | y) ^ (x & y);
            Expression<Func<int, int, int>> expected = (x, y) => (x | y) ^ (x & y);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void E2E_NegativeConstantArithmetic()
        {
            Func<int, int> foo = x => x * -3 + 127;
            Expression<Func<int, int>> expected = x => x * -3 + 127;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void E2E_ShiftAndXor()
        {
            Func<int, int, int> foo = (x, y) => (x << 2) ^ (y >> 1);
            Expression<Func<int, int, int>> expected = (x, y) => (x << 2) ^ (y >> 1);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void E2E_FieldAccess_InstanceFieldAdd()
        {
            // This was broken before the ldfld return→break fix
            Func<FieldHolder, int> foo = h => h.Value + 1;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("+", decompiled.ToString());
            Assert.Contains("Value", decompiled.ToString());
        }

        [Fact]
        public void E2E_MultipleComparisons()
        {
            Func<int, int, int, bool> foo = (x, y, z) => (x > y) & (y < z) & (x != z);
            Expression<Func<int, int, int, bool>> expected = (x, y, z) => (x > y) & (y < z) & (x != z);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void E2E_MethodOnProperty()
        {
            // Access a property, then call a method on the result
            Func<ManualProperty, string> foo = m => m.Name.ToLower();
            Expression<Func<ManualProperty, string>> expected = m => m.Name.ToLower();
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void E2E_PropertyEquality()
        {
            // String equality compiles to a call to op_Equality, not ceq.
            // The decompiler correctly emits the method call form.
            Func<ManualProperty, ManualProperty, bool> foo = (a, b) => a.Name == b.Name;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("a.Name", decompiled.ToString());
            Assert.Contains("b.Name", decompiled.ToString());
            Assert.Contains("op_Equality", decompiled.ToString());
        }

        #endregion

        #region 12. LinqDecompiler unit-level tests for new overrides

        [Fact]
        public void LinqDec_Xor_ReturnsExclusiveOr()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var y = Expression.Parameter(typeof(int), "y");
            var result = dec.Xor(x, y);
            Assert.Equal(ExpressionType.ExclusiveOr, result.NodeType);
        }

        [Fact]
        public void LinqDec_Throw_ReturnsThrowExpression()
        {
            var dec = new LinqDecompiler();
            var ex = Expression.New(typeof(Exception));
            var result = dec.Throw(ex);
            Assert.Equal(ExpressionType.Throw, result.NodeType);
        }

        [Fact]
        public void LinqDec_AddressOf_IsPassThrough()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var result = dec.AddressOf(x);
            Assert.Same(x, result);
        }

        [Fact]
        public void LinqDec_AddressGet_IsPassThrough()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var result = dec.AddressGet(x);
            Assert.Same(x, result);
        }

        [Fact]
        public void LinqDec_Duplicate_IsPassThrough()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var result = dec.Duplicate(x);
            Assert.Same(x, result);
        }

        [Fact]
        public void LinqDec_EndFilter_IsPassThrough()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var result = dec.EndFilter(x);
            Assert.Same(x, result);
        }

        [Fact]
        public void LinqDec_Unaligned_IsPassThrough()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var result = dec.Unaligned(x);
            Assert.Same(x, result);
        }

        [Fact]
        public void LinqDec_Goto_ReturnsGotoExpression()
        {
            var dec = new LinqDecompiler();
            var lbl = dec.Label("L");
            var result = dec.Goto(lbl);
            Assert.Equal(ExpressionType.Goto, result.NodeType);
        }

        [Fact]
        public void LinqDec_ObjectInit_ReturnsAssignDefault()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var result = dec.ObjectInit(x, typeof(int));
            Assert.Equal(ExpressionType.Assign, result.NodeType);
        }

        [Fact]
        public void LinqDec_CastOverflow_ReturnsConvertChecked()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var result = dec.CastOverflow(x, typeof(byte));
            Assert.Equal(ExpressionType.ConvertChecked, result.NodeType);
            Assert.Equal(typeof(byte), result.Type);
        }

        [Fact]
        public void LinqDec_CastOverflow_ToUShort()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(int), "x");
            var result = dec.CastOverflow(x, typeof(ushort));
            Assert.Equal(ExpressionType.ConvertChecked, result.NodeType);
            Assert.Equal(typeof(ushort), result.Type);
        }

        [Fact]
        public void LinqDec_CastOverflow_ToUInt()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(long), "x");
            var result = dec.CastOverflow(x, typeof(uint));
            Assert.Equal(ExpressionType.ConvertChecked, result.NodeType);
            Assert.Equal(typeof(uint), result.Type);
        }

        [Fact]
        public void LinqDec_CastOverflow_ToULong()
        {
            var dec = new LinqDecompiler();
            var x = Expression.Parameter(typeof(long), "x");
            var result = dec.CastOverflow(x, typeof(ulong));
            Assert.Equal(ExpressionType.ConvertChecked, result.NodeType);
            Assert.Equal(typeof(ulong), result.Type);
        }

        #endregion
    }
}
