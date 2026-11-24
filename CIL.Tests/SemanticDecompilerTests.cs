using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;
using CIL.Expressions;
using static CIL.Tests.Equivalence;

namespace CIL.Tests
{
    /// <summary>
    /// End-to-end decompilation tests that compile the decompiled expression and check it
    /// computes the same results as the original method. Each region covers a class of bugs
    /// in IL.Process that structural tests did not detect.
    /// </summary>
    public class SemanticDecompilerTests
    {
        static readonly object[][] Ints = Each(-1000, -5, -1, 0, 1, 2, 7, 11, 100, int.MinValue, int.MaxValue);
        static readonly object[][] Small = Each(-3, 0, 1, 2, 5, 9, 12);
        static readonly object[][] Strings = Each(null, "", "12", "abc", "99999999999");
        static readonly object[][] Doubles = (from x in new[] { -1.5, 0, 2.0, double.NaN, double.PositiveInfinity }
                                              from y in new[] { -1.5, 0, 2.0, double.NaN }
                                              select new object[] { x, y }).ToArray();
        static readonly object[][] IntPairs = (from x in new[] { -1000, -1, 0, 1, 7, int.MinValue }
                                               from y in new[] { -3, 0, 1, 5 }
                                               select new object[] { x, y }).ToArray();

        static int counter;
        static int Bump() { counter++; return 10; }
        static object Counter() => counter;
        static void Reset() => counter = 0;

        #region Constants and operand decoding

        static long Big() => 5000000000L;
        static long AddBig(long x) => x + 3000000000L;
        static Type TypeOfString() => typeof(string);
        static DayOfWeek Friday() => DayOfWeek.Friday;
        static string ReturnNull() => null;
        static bool PassNull() => string.IsNullOrEmpty(null);

        [Fact] public void Ldc_i8_IsNotTruncated() => Check(new Func<long>(Big));
        [Fact] public void Ldc_i8_InArithmetic() => Check(new Func<long, long>(AddBig), Each(1L, -7L, long.MaxValue));
        [Fact] public void Ldtoken_TypeOf() => Check(new Func<Type>(TypeOfString));
        [Fact] public void EnumConstant_Return() => Check(new Func<DayOfWeek>(Friday));
        [Fact] public void NullConstant_Return() => Check(new Func<string>(ReturnNull));
        [Fact] public void NullConstant_Argument() => Check(new Func<bool>(PassNull));

        #endregion

        #region Signedness, unordered comparisons and stack typing

        static int ShrUn(int x) => (int)((uint)x >> 1);
        static int DivUn(int x, int y) => (int)((uint)x / (uint)y);
        static int RemUn(int x, int y) => (int)((uint)x % (uint)y);
        static bool LessUnsigned(int x, int y) => (uint)x < (uint)y;
        static double UIntToDouble(uint x) => x;
        static long ZeroExtend(int x) => (long)(uint)x;
        static long SignExtend(uint x) => (long)(int)x;
        static bool DGe(double x, double y) => x >= y;
        static bool DLe(double x, double y) => x <= y;
        static int DLtBranch(double x, double y) { if (x < y) return 1; return 0; }
        static int DGtBranch(double x, double y) { if (x > y) return 1; return 0; }
        static char NextChar(char c) => (char)(c + 1);
        static bool IsA(char c) => c == 'a';
        static bool IsFriday(DayOfWeek d) => d == DayOfWeek.Friday;
        static bool NotNull(string s) => s != null;
        static bool IsString(object o) => o is string;

        [Fact] public void ShrUn_IsLogicalShift() => Check(new Func<int, int>(ShrUn), Ints);
        [Fact] public void DivUn_IsUnsigned() => Check(new Func<int, int, int>(DivUn), (from x in new[] { -7, 7, int.MinValue } from y in new[] { 2, -2 } select new object[] { x, y }).ToArray());
        [Fact] public void RemUn_IsUnsigned() => Check(new Func<int, int, int>(RemUn), (from x in new[] { -7, 7, int.MinValue } from y in new[] { 3, -3 } select new object[] { x, y }).ToArray());
        [Fact] public void CltUn_IsUnsigned() => Check(new Func<int, int, bool>(LessUnsigned), IntPairs);
        [Fact] public void ConvRUn_IsDoublePrecision() => Check(new Func<uint, double>(UIntToDouble), Each(0u, 0x7FFFFFFFu, uint.MaxValue));
        [Fact] public void ConvU8_ZeroExtends() => Check(new Func<int, long>(ZeroExtend), Ints);
        [Fact] public void ConvI8_SignExtends() => Check(new Func<uint, long>(SignExtend), Each(0u, 0x80000000u, uint.MaxValue));
        [Fact] public void CltUn_OnDoubles_IsUnordered() => Check(new Func<double, double, bool>(DGe), Doubles);
        [Fact] public void CgtUn_OnDoubles_IsUnordered() => Check(new Func<double, double, bool>(DLe), Doubles);
        [Fact] public void BgeUn_OnDoubles_IsUnordered() => Check(new Func<double, double, int>(DLtBranch), Doubles);
        [Fact] public void BleUn_OnDoubles_IsUnordered() => Check(new Func<double, double, int>(DGtBranch), Doubles);
        [Fact] public void CharArithmetic() => Check(new Func<char, char>(NextChar), Each('a', '￿'));
        [Fact] public void CharComparison() => Check(new Func<char, bool>(IsA), Each('a', 'b'));
        [Fact] public void EnumComparison() => Check(new Func<DayOfWeek, bool>(IsFriday), Each(DayOfWeek.Friday, DayOfWeek.Monday));
        [Fact] public void CgtUn_NotNullIdiom() => Check(new Func<string, bool>(NotNull), Strings);
        [Fact] public void Isinst_NotNullIdiom() => Check(new Func<object, bool>(IsString), Each("a", 1, null));

        #endregion

        #region Calls and coercion of arguments

        static string MixedImpl(bool b, char c) => b ? c.ToString() : "-";
        static string Mixed(bool b, char c) => MixedImpl(b, c);
        static long MixedLongImpl(int a, long b) => a * 10L + b;
        static long MixedLong(int a) => MixedLongImpl(a, 5);
        static string NewString(int n) => new string('a', n);
        static int Digits(int a, int b, int c) => a * 100 + b * 10 + c;
        static int StackOrder(int a, int b, int c) => Digits(a, b, c > 0 ? 1 : 2);
        static int TryParseOut(string s) => int.TryParse(s, out var v) ? v : -1;

        class PropHolder { public int Prop { get; set; } }
        static int SetProperty(int x) { var p = new PropHolder(); p.Prop = x; return p.Prop; }

        class FlagHolder { public bool Flag; }
        static bool SetBoolField() { var h = new FlagHolder(); h.Flag = true; return h.Flag; }

        [Fact] public void Call_CoercesEachArgumentToItsOwnParameter() => Check(new Func<bool, char, string>(Mixed), new object[] { true, 'x' }, new object[] { false, 'y' });
        [Fact] public void Call_WidensArgument() => Check(new Func<int, long>(MixedLong), Each(3, -3));
        [Fact] public void Newobj_CoercesArguments() => Check(new Func<int, string>(NewString), Each(0, 3));
        [Fact] public void Call_PreservesArgumentOrderAcrossBranch() => Check(new Func<int, int, int, int>(StackOrder), (from c in new[] { -1, 0, 1 } select new object[] { 1, 2, c }).ToArray());
        [Fact] public void Call_OutParameter() => Check(new Func<string, int>(TryParseOut), Strings);
        [Fact] public void PropertySetter() => Check(new Func<int, int>(SetProperty), Each(0, 7));
        [Fact] public void BoolField_StoreConstant() => Check(new Func<bool>(SetBoolField));

        #endregion

        #region Evaluation order and single evaluation

        class Holder { public int Value; }
        static int ObjectInitializer() => new Holder { Value = 5 }.Value;
        static int ChainAssign(int x) { int a, b; a = b = x * 2; return a + b; }
        static int PostIncrement(int x) { int y = x++; return x + y; }
        static int SideEffectBeforeBranch(int x) => Bump() + (x > 0 ? 1 : 2);
        static int DiscardedCall(int x) { Bump(); return x; }
        static int StaticIncrement(int x) { counter++; int y = counter++ + x; return y * 10 + counter; }
        static int ArrayPostIncrement(int x) { var a = new int[4]; int i = 0; a[i++] = x; a[i++] = x * 2; return a[0] + a[1] * 10 + i * 100; }
        static int ArrayCompound(int x) { var a = new int[3]; a[1] += x; a[2] = a[1]++ * 2; return a[0] + a[1] * 10 + a[2] * 100; }
        static int TernaryCalls(int x) => (x > 0 ? Bump() : -Bump()) + (x > 5 ? Bump() * 2 : 0);

        [Fact] public void Dup_ObjectInitializer() => Check(new Func<int>(ObjectInitializer));
        [Fact] public void Dup_ChainedAssignment() => Check(new Func<int, int>(ChainAssign), Small);
        [Fact] public void Dup_PostIncrementReadsOldValue() => Check(new Func<int, int>(PostIncrement), Small);
        [Fact] public void SideEffect_EvaluatedOnceBeforeBranch() => Check(new Func<int, int>(SideEffectBeforeBranch), Counter, Reset, Small);
        [Fact] public void Pop_KeepsSideEffect() => Check(new Func<int, int>(DiscardedCall), Counter, Reset, Small);
        [Fact] public void StaticField_PostIncrement() => Check(new Func<int, int>(StaticIncrement), Counter, Reset, Small);
        [Fact] public void ArrayElement_PostIncrementIndex() => Check(new Func<int, int>(ArrayPostIncrement), Small);
        [Fact] public void ArrayElement_CompoundAssignment() => Check(new Func<int, int>(ArrayCompound), Small);
        [Fact] public void Ternary_CallsInBranches() => Check(new Func<int, int>(TernaryCalls), Counter, Reset, Small);

        #endregion

        #region Branches

        static int Abs(int x) => x > 0 ? x : -x;
        static int Clamp(int x) => x > 0 ? (x > 100 ? 100 : x) : (x < -100 ? -100 : x);
        static int Sign(int x) { if (x > 0) return 1; if (x < 0) return -1; return 0; }
        static string Coalesce(string s) => s ?? "default";
        static int LengthOrZero(string s) => s != null ? s.Length : 0;
        static int IfNoElse(int x) { if (x > 0) x = x * 2; return x; }
        static int IfElseMerge(int x) { if (x > 0) x = 1; else x = 2; return x * 3; }
        static int ValueAcrossBranch(int x, int y) => y + (x > 0 ? 1 : 2);
        static bool And3(int x, int y, int z) => x > 0 && y > 0 && z > 0;
        static bool Or3(int x, int y, int z) => x > 0 || y > 0 || z > 0;
        static bool IsPositive(int x) { if (x > 0) return true; return false; }
        static int ThrowIfNegative(int x) { if (x < 0) throw new ArgumentException("neg"); return x; }
        static int ShortCircuitSkipsCall(int x) => x > 0 && Bump() > 5 ? 1 : 0;
        static int Sequential(int x)
        {
            int s = 0;
            if (x > 1) s += 1; if (x > 2) s += 2; if (x > 3) s += 4; if (x > 4) s += 8; if (x > 5) s += 16;
            if (x > 6) s += 32; if (x > 7) s += 64; if (x > 8) s += 128; if (x > 9) s += 256; if (x > 10) s += 512;
            if (x > 11) s += 1024; if (x > 12) s += 2048; if (x > 13) s += 4096; if (x > 14) s += 8192; if (x > 15) s += 16384;
            if (x > 16) s += 32768; if (x > 17) s += 65536; if (x > 18) s += 131072; if (x > 19) s += 262144; if (x > 20) s += 524288;
            return s;
        }
        static readonly object[][] Triples = (from x in new[] { -1, 1 } from y in new[] { 0, 2 } from z in new[] { -5, 3 } select new object[] { x, y, z }).ToArray();

        [Fact] public void Ternary() => Check(new Func<int, int>(Abs), Ints);
        [Fact] public void NestedTernary() => Check(new Func<int, int>(Clamp), Ints);
        [Fact] public void MultipleReturns() => Check(new Func<int, int>(Sign), Ints);
        [Fact] public void NullCoalescing() => Check(new Func<string, string>(Coalesce), Strings);
        [Fact] public void NullConditional() => Check(new Func<string, int>(LengthOrZero), Strings);
        [Fact] public void IfWithoutElse() => Check(new Func<int, int>(IfNoElse), Ints);
        [Fact] public void IfElse_ThenMerge() => Check(new Func<int, int>(IfElseMerge), Ints);
        [Fact] public void Ternary_ValueAcrossBranch() => Check(new Func<int, int, int>(ValueAcrossBranch), IntPairs);
        [Fact] public void ShortCircuitAnd() => Check(new Func<int, int, int, bool>(And3), Triples);
        [Fact] public void ShortCircuitOr() => Check(new Func<int, int, int, bool>(Or3), Triples);
        [Fact] public void BoolReturnsFromBranches() => Check(new Func<int, bool>(IsPositive), Ints);
        [Fact] public void ThrowInBranch() => Check(new Func<int, int>(ThrowIfNegative), Ints);
        [Fact] public void ShortCircuit_SkipsSideEffect() => Check(new Func<int, int>(ShortCircuitSkipsCall), Counter, Reset, Small);

        [Fact]
        public void SequentialIfs_MergeInsteadOfDuplicating()
        {
            // 20 sequential ifs have 2^20 paths; this only terminates quickly if the
            // decompiler rejoins the arms of each if rather than duplicating the tail
            Check(new Func<int, int>(Sequential), Enumerable.Range(0, 23).Select(x => new object[] { x }).ToArray());
        }

        #endregion

        #region Loops

        static int ForSum(int n) { int s = 0; for (int i = 0; i < n; i++) s += i; return s; }
        static int WhileDown(int n) { int c = 0; while (n > 0) { n--; c += 2; } return c; }
        static int DoWhile(int n) { int i = 0; do { i++; } while (i < n); return i; }
        static int WhileTrueReturn(int x) { while (true) { if (x > 10) return x; x++; } }
        static int ForIf(int n) { int s = 0; for (int i = 0; i < n; i++) { if (i % 2 == 0) s += i; } return s; }
        static int Nested(int n, int m) { int s = 0; for (int i = 0; i < n; i++) for (int j = 0; j < m; j++) s++; return s; }
        static int MultipleExits(int n) { int s = 0; for (int i = 0; i < 100; i++) { if (i >= n) break; if (i == 7) return -1; s += i; } return s; }
        static int WhileOr(string s) { int i = 0; while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++; return i; }
        static int WhileAnd(int n) { int i = 0, s = 0; while (i < n && s < 50) { s += i; i++; } return s * 100 + i; }
        static int DoBreak(int n) { int i = 0; do { if (i * i > n) break; i++; } while (i < 100); return i; }
        static int InfiniteThrow(int x) { while (true) { if (x > 10) throw new InvalidOperationException("x"); x++; } }
        static int GotoLoop(int x) { int i = 0; top: i++; if (i < x) goto top; return i; }
        static int GotoIntoLoop(int x) { int i = 0; if (x > 3) goto mid; top: i += 2; mid: i += 1; if (i < x) goto top; return i; }
        static void IncrementN(int n) { for (int i = 0; i < n; i++) counter++; }
        static int ForeachList(int n) { var l = new List<int>(); for (int i = 0; i < n; i++) l.Add(i * i); int s = 0; foreach (var x in l) s += x; return s; }
        static int VowelCount(string s) { if (s == null) return 0; int c = 0; foreach (var ch in s) if ("aeiou".IndexOf(ch) >= 0) c++; return c; }
        static int InnerLoopFirst(int n)
        {
            // the outer loop's body begins with an inner loop, so both share a header
            int i = 0, j = 0;
            while (i < n) { while (j < i * 2) j++; i++; }
            return i * 1000 + j;
        }

        [Fact] public void ForLoop() => Check(new Func<int, int>(ForSum), Small);
        [Fact] public void WhileLoop_TestsBeforeFirstIteration() => Check(new Func<int, int>(WhileDown), Small);
        [Fact] public void DoWhileLoop_RunsBodyOnceBeforeTest() => Check(new Func<int, int>(DoWhile), Small);
        [Fact] public void WhileTrue_WithReturn() => Check(new Func<int, int>(WhileTrueReturn), Small);
        [Fact] public void LoopContainingIf() => Check(new Func<int, int>(ForIf), Small);
        [Fact] public void NestedLoops() => Check(new Func<int, int, int>(Nested), new object[] { 0, 3 }, new object[] { 2, 3 }, new object[] { 3, 0 });
        [Fact] public void Loop_BreakAndReturn() => Check(new Func<int, int>(MultipleExits), Each(0, 3, 7, 8, 50));
        [Fact] public void Loop_CompoundCondition() => Check(new Func<string, int>(WhileOr), Each("", "x", "  \t x", "\t\t"));
        [Fact] public void Loop_AndCondition() => Check(new Func<int, int>(WhileAnd), Small);
        [Fact] public void DoWhile_Break() => Check(new Func<int, int>(DoBreak), Each(0, 5, 50, 100000));
        [Fact] public void Loop_OnlyExitsByThrowing() => Check(new Func<int, int>(InfiniteThrow), Small);
        [Fact] public void Goto_BackwardLoop() => Check(new Func<int, int>(GotoLoop), Small);
        [Fact] public void Goto_IntoMiddleOfLoop() => Check(new Func<int, int>(GotoIntoLoop), Small);
        [Fact] public void VoidMethod_Loop() => Check(new Action<int>(IncrementN), Counter, Reset, Small);
        [Fact] public void Foreach_List() => Check(new Func<int, int>(ForeachList), Small);
        [Fact] public void Foreach_String() => Check(new Func<string, int>(VowelCount), Each(null, "", "hello", "aeiou x"));
        [Fact] public void Loops_SharingAHeader() => Check(new Func<int, int>(InnerLoopFirst), Small);

        #endregion

        #region Switch

        static int Switch3(int x) { switch (x) { case 0: return 10; case 1: return 20; case 2: return 30; default: return -1; } }
        static int StringSwitch(string s) { switch (s) { case "a": return 1; case "bb": return 2; case "ccc": return 3; case "dddd": return 4; case "eeeee": return 5; case "ffffff": return 6; case "g": return 7; default: return -1; } }
        static string SwitchExpression(int x) => x switch { 0 => "zero", 1 => "one", < 0 => "neg", _ => "many" };
        static int GotoCase(int x) { int r = 0; switch (x) { case 0: r += 1; goto case 1; case 1: r += 10; break; case 2: r += 100; goto default; default: r += 1000; break; } return r; }
        static int SwitchInLoop(int n)
        {
            int s = 0;
            for (int i = 0; i < n; i++)
            {
                switch (i % 4)
                {
                    case 0: s += 1; break;
                    case 1: continue;
                    case 2: s += 100; break;
                    default: if (i > 6) goto done; s += 10; break;
                }
                s *= 2;
            }
        done:
            return s;
        }

        [Fact] public void Switch_WithDefault() => Check(new Func<int, int>(Switch3), Each(-1, 0, 1, 2, 3));
        [Fact] public void Switch_OnString() => Check(new Func<string, int>(StringSwitch), Each(null, "", "a", "bb", "ccc", "dddd", "eeeee", "ffffff", "g", "zz"));
        [Fact] public void Switch_Expression() => Check(new Func<int, string>(SwitchExpression), Ints);
        [Fact] public void Switch_GotoCase() => Check(new Func<int, int>(GotoCase), Each(-1, 0, 1, 2, 3));
        [Fact] public void Switch_InLoopWithContinue() => Check(new Func<int, int>(SwitchInLoop), Enumerable.Range(0, 12).Select(x => new object[] { x }).ToArray());

        #endregion

        #region Exception handling

        static int TryCatchFinally(string s) { int r = 0; try { r = int.Parse(s); } catch (FormatException) { r = -1; } finally { r += 100; } return r; }
        static int NestedTry(string s) { try { try { return int.Parse(s); } catch (FormatException) { return -1; } } catch (Exception) { return -99; } }
        static int CatchVariable(string s) { try { return int.Parse(s); } catch (FormatException ex) { return ex.Message.Length > 0 ? -1 : -2; } }
        static int Rethrow(string s) { try { return int.Parse(s); } catch (FormatException) { throw; } }
        static int Filter(string s) { try { return int.Parse(s); } catch (Exception ex) when (ex is FormatException) { return -1; } }
        static int ReturnFromTryAndAfter(string s) { try { return int.Parse(s); } catch (FormatException) { } return -1; }
        static int CatchContinue(string[] a)
        {
            int s = 0;
            if (a == null) return -9;
            for (int i = 0; i < a.Length; i++)
            {
                try { s += int.Parse(a[i]); }
                catch (FormatException) { if (i > 5) break; continue; }
                s += 1000;
            }
            return s;
        }
        static int UsingStatement(int n) { int r; using (var m = new System.IO.MemoryStream()) { m.WriteByte((byte)n); r = (int)m.Length; } return r; }
        static readonly object gate = new object();
        static int LockStatement(int n) { lock (gate) { return n * 2; } }
        static readonly object[][] StringArrays =
        {
            new object[] { null }, new object[] { new string[0] }, new object[] { new[] { "1", "x", "3" } },
            new object[] { new[] { "1", "2", "y", "z", "5", "6", "w", "8", "9" } },
        };

        [Fact] public void TryCatchFinally_RunsFinally() => Check(new Func<string, int>(TryCatchFinally), Strings);
        [Fact] public void NestedTry_SameStart() => Check(new Func<string, int>(NestedTry), Strings);
        [Fact] public void Catch_BindsExceptionVariable() => Check(new Func<string, int>(CatchVariable), Strings);
        [Fact] public void Catch_Rethrow() => Check(new Func<string, int>(Rethrow), Strings);
        [Fact] public void Catch_Filter() => Check(new Func<string, int>(Filter), Strings);
        [Fact] public void Try_LeaveTargetsDiffer() => Check(new Func<string, int>(ReturnFromTryAndAfter), Strings);
        [Fact] public void Catch_ContinueAndBreak() => Check(new Func<string[], int>(CatchContinue), StringArrays);
        [Fact] public void Using() => Check(new Func<int, int>(UsingStatement), Small);
        [Fact] public void Lock() => Check(new Func<int, int>(LockStatement), Small);

        #endregion

        #region Arrays and value types

        struct Point { public int X, Y; public void Move(int d) { X += d; Y -= d; } }
        static int ArrayLength(int n) => new int[n].Length;
        static int StructMutation(int d) { var p = new Point(); p.X = 5; p.Move(d); return p.X * 100 + p.Y; }
        static int StructElementMutation(int d) { var a = new Point[2]; a[1].Move(d); return a[1].X - a[1].Y; }
        static int NullableAdd(int x) { int? a = x > 0 ? x : (int?)null; int? b = a + 1; return b ?? -100; }
        static int BitwiseNot(int x) => ~x;

        [Fact] public void Newarr_TakesLength() => Check(new Func<int, int>(ArrayLength), Each(0, 3));
        [Fact] public void Struct_MutatedThroughLocalAddress() => Check(new Func<int, int>(StructMutation), Small);
        [Fact] public void Struct_MutatedThroughElementAddress() => Check(new Func<int, int>(StructElementMutation), Small);
        [Fact] public void Nullable_Arithmetic() => Check(new Func<int, int>(NullableAdd), Small);
        [Fact] public void Not_IsBitwiseComplement() => Check(new Func<int, int>(BitwiseNot), Ints);

        #endregion

        #region Unsupported code is rejected rather than mistranslated

        static Func<int> MakeDelegate() => () => 1;

        [Fact]
        public void DelegateCreation_IsNotSupported()
        {
            // ldftn can't be expressed as a LINQ expression
            Assert.Throws<NotSupportedException>(() => LinqDecompiler.Decompile(new Func<Func<int>>(MakeDelegate)));
        }

        #endregion

        #region Regression tests — boxing, unification, pattern matching, irreducible CFG, guards

        static object BoxBool(bool b) => b;
        static object BoxTrue() => true;
        static object BoxChar() => 'a';
        static object BoxEnum() => DayOfWeek.Wednesday;
        static object BoxTernary(bool flag, int x) => flag ? (object)true : (object)x;
        static int UnifyBoolAndInt(bool flag, int a, int b, int y) => flag ? (a == b ? 1 : 0) : y;
        static object GetBoxed(int k) => k == 0 ? (object)42 : k == 1 ? "str" : null;
        static int PatternMatchInt(int k) => GetBoxed(k) is int v ? v + 1 : -1;
        static int ChainedElementFieldCompound(int x)
        {
            var a = new Point[3];
            int idx = 0;
            a[idx++].X += x;
            a[idx++].Y -= x * 2;
            return a[0].X * 100 + a[1].Y * 10 + idx;
        }
        static int IrreducibleSkipMid(int x)
        {
            int i = 0;
            if (x > 3) goto mid;
        top:
            i += 2;
            if (i > 10) goto after_mid;
        mid:
            i += 1;
        after_mid:
            if (i < x) goto top;
            return i;
        }
        static int SequentialIfWithInnerReturn(int x)
        {
            int s = 0;
            if (x > 1) { if (x == 99) return -1; s += 1; }
            if (x > 2) { if (x == 98) return -2; s += 2; }
            if (x > 3) { if (x == 97) return -3; s += 4; }
            if (x > 4) { if (x == 96) return -4; s += 8; }
            if (x > 5) { if (x == 95) return -5; s += 16; }
            if (x > 6) { if (x == 94) return -6; s += 32; }
            if (x > 7) { if (x == 93) return -7; s += 64; }
            if (x > 8) { if (x == 92) return -8; s += 128; }
            if (x > 9) { if (x == 91) return -9; s += 256; }
            if (x > 10) { if (x == 90) return -10; s += 512; }
            if (x > 11) { if (x == 89) return -11; s += 1024; }
            if (x > 12) { if (x == 88) return -12; s += 2048; }
            if (x > 13) { if (x == 87) return -13; s += 4096; }
            if (x > 14) { if (x == 86) return -14; s += 8192; }
            if (x > 15) { if (x == 85) return -15; s += 16384; }
            if (x > 16) { if (x == 84) return -16; s += 32768; }
            if (x > 17) { if (x == 83) return -17; s += 65536; }
            if (x > 18) { if (x == 82) return -18; s += 131072; }
            if (x > 19) { if (x == 81) return -19; s += 262144; }
            if (x > 20) { if (x == 80) return -20; s += 524288; }
            return s;
        }
        static string ConstrainedStructCall(int x) => x.ToString();
        static Type ConstrainedValueTypeGetType(int x) => x.GetType();
        class OpenTarget { public int Offset; public int Add(int x) => Offset + x; }

        [Fact] public void Box_BoolConstant_ReturnsBoxedBool() => Check(new Func<object>(BoxTrue));
        [Fact] public void Box_BoolParam_ReturnsBoxedBool() => Check(new Func<bool, object>(BoxBool), Each(true, false));
        [Fact] public void Box_CharConstant_ReturnsBoxedChar() => Check(new Func<object>(BoxChar));
        [Fact] public void Box_EnumConstant_ReturnsBoxedEnum() => Check(new Func<object>(BoxEnum));
        [Fact] public void Box_TernaryMixedTypes() => Check(new Func<bool, int, object>(BoxTernary), new object[] { true, 5 }, new object[] { false, 5 });
        [Fact] public void UnifyType_BoolAndInt_PreservesIntValue() => Check(new Func<bool, int, int, int, int>(UnifyBoolAndInt),
            new object[] { true, 1, 1, 42 }, new object[] { true, 1, 2, 42 }, new object[] { false, 1, 1, 42 }, new object[] { false, 1, 2, -7 });
        [Fact] public void PatternMatch_IsInstThenUnboxAny() => Check(new Func<int, int>(PatternMatchInt), Each(0, 1, 2));
        [Fact] public void StructArrayElementField_CompoundAssignment() => Check(new Func<int, int>(ChainedElementFieldCompound), Small);
        [Fact] public void IrreducibleLoop_SkippingMid_IsEquivalent() => Check(new Func<int, int>(IrreducibleSkipMid), Each(0, 1, 3, 4, 8, 15, 25));
        [Fact] public void SequentialIfWithInnerReturn_NoExponentialBlowup() => Check(new Func<int, int>(SequentialIfWithInnerReturn), Each(0, 5, 21, 80, 90, 99));
        [Fact] public void Constrained_ValueType_ToString() => Check(new Func<int, string>(ConstrainedStructCall), Small);
        [Fact] public void Constrained_ValueType_GetType() => Check(new Func<int, Type>(ConstrainedValueTypeGetType), Each(0, 42));
        [Fact]
        public void OpenInstanceDelegate_Decompiles()
        {
            var m = typeof(OpenTarget).GetMethod(nameof(OpenTarget.Add));
            var open = (Func<OpenTarget, int, int>)Delegate.CreateDelegate(typeof(Func<OpenTarget, int, int>), m);
            Check(open, new object[] { new OpenTarget { Offset = 10 }, 5 }, new object[] { new OpenTarget { Offset = -3 }, 7 });
        }

        #endregion

        #region Completeness — CallSignature, ldftn, ref locals/ternaries, ctor chaining, TypedReference, metadata

        /// <summary>
        /// Minimal non-LINQ tagless-final visitor (T = string, no TypeOf/IsConstant/Value)
        /// verifying decoder support for CIL features beyond LINQ Expressions.
        /// </summary>
        sealed class StringVisitor : ExpressionError<string>
        {
            public override string Param(System.Reflection.ParameterInfo x) => x.Name;
            public override string Param(string name, Type type) => name;
            public override string Local(System.Reflection.LocalVariableInfo x) => $"loc{x.LocalIndex}";
            public override string Constant<TValue>(TValue value) => value?.ToString() ?? "null";
            public override string Constant(object value, Type type) => value?.ToString() ?? "null";
            public override string Add(string l, string r) => $"({l} + {r})";
            public override string Subtract(string l, string r) => $"({l} - {r})";
            public override string Multiply(string l, string r) => $"({l} * {r})";
            public override string LessThan(string l, string r) => $"({l} < {r})";
            public override string GreaterThan(string l, string r) => $"({l} > {r})";
            public override string Equal(string l, string r) => $"({l} == {r})";
            public override string NotEqual(string l, string r) => $"({l} != {r})";
            public override string Not(string e) => $"(!{e})";
            public override string Cast(string e, Type castTo) => $"(({castTo.Name}){e})";
            public override string Assign(string v, string val) => $"{v} = {val}";
            public override string Duplicate(string e) => e;
            public override string AddressOf(string b) => $"&{b}";
            public override string AddressGet(string a) => $"*{a}";
            public override string AddressSet(string a, string v) => $"*{a} = {v}";
            public override string ArrayGet(string a, string i) => $"{a}[{i}]";
            public override string Field(System.Reflection.FieldInfo f, string o) => o == null ? f.Name : $"{o}.{f.Name}";
            public override string If(string c, string t, string e) => $"({c} ? {t} : {e})";
            public override string If(string c, string t) => $"if ({c}) {{ {t} }}";
            public override string Block(IEnumerable<string> e) => string.Join("; ", e);
            public override string Return() => "return";
            public override string Return(string e) => $"return {e}";
            public override string Label(string name) => name;
            public override string MarkLabel(string lbl) => $"{lbl}:";
            public override string Goto(string lbl) => $"goto {lbl}";
            public override string Break(string lbl) => $"break {lbl}";
            public override string Continue(string lbl) => $"continue {lbl}";
            public override string Loop(string body, string brk, string cont) => $"loop {{ {body} }}";
            public override string Call(System.Reflection.MethodBase m, bool tail, string o, IEnumerable<string> args) =>
                $"{(o == null ? "" : o + ".")}{m.Name}({string.Join(", ", args)})";
            public override string New(System.Reflection.ConstructorInfo c, IEnumerable<string> args) =>
                $"new {c.DeclaringType.Name}({string.Join(", ", args)})";
            public override string GetPointer(System.Reflection.MethodBase m, string o) =>
                o == null ? $"&{m.Name}" : $"&{o}.{m.Name}";
            public override string CallIndirect(byte[] sig, string ftn, bool tail, IEnumerable<string> args) =>
                $"calli({ftn})({string.Join(", ", args)})";
            public override string MakeRefAny(string addr, Type type) => $"mkrefany<{type.Name}>({addr})";
            public override string RefAnyValue(string tr, Type type) => $"refanyval<{type.Name}>({tr})";
            public override string RefAnyType(string tr) => $"refanytype({tr})";
        }

        static int RefLocalAndTernary(int[] arr, int a, int b)
        {
            ref int r = ref (a > b ? ref arr[0] : ref arr[1]);
            r = r + 10;
            return r;
        }

        static int RebindRefParam(ref int x, ref int y, bool pickY)
        {
            if (pickY)
                x = ref y;
            x = 99;
            return x;
        }

        static int UseTypedRef(int x)
        {
            TypedReference tr = __makeref(x);
            Type t = __reftype(tr);
            __refvalue(tr, int) = 42;
            return t == typeof(int) ? x : -1;
        }

        class CtorBase { public int V; public CtorBase(int v) { V = v; } }
        class CtorDerived : CtorBase { public CtorDerived(int x) : base(x + 1) { V += 2; } }

        [Fact]
        public void RefLocalAndConditionalRef_DecodesWithCustomVisitor()
        {
            var m = typeof(SemanticDecompilerTests).GetMethod(nameof(RefLocalAndTernary), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var text = m.Decompile(new StringVisitor(), out _);
            Assert.Contains("&arr[0]", text);
            Assert.Contains("&arr[1]", text);
            Assert.Contains("+", text);
            Assert.Throws<NotSupportedException>(() => LinqDecompiler.Decompile(new Func<int[], int, int, int>(RefLocalAndTernary)));
        }

        [Fact]
        public void RefParamRebind_DecodesWithCustomVisitor()
        {
            var m = typeof(SemanticDecompilerTests).GetMethod(nameof(RebindRefParam), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var text = m.Decompile(new StringVisitor(), out _);
            Assert.Contains("x = y", text);
            Assert.Contains("*x = 99", text);
        }

        [Fact]
        public void TypedReference_MkRefAny_RefAnyVal_RefAnyType_Decodes()
        {
            var m = typeof(SemanticDecompilerTests).GetMethod(nameof(UseTypedRef), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var text = m.Decompile(new StringVisitor(), out _);
            Assert.Contains("mkrefany<Int32>", text);
            Assert.Contains("refanyval<Int32>", text);
            Assert.Contains("refanytype", text);
        }

        [Fact]
        public void Ldftn_DecodesInBothDebugAndRelease()
        {
            var m = new Func<Func<int>>(MakeDelegate).Method;
            var text = m.Decompile(new StringVisitor(), out _);
            Assert.Contains("&", text);
        }

        [Fact]
        public void ConstructorChaining_DecodesWithCustomVisitor()
        {
            var ctor = typeof(CtorDerived).GetConstructor(new[] { typeof(int) });
            var text = ctor.Decompile(new StringVisitor(), out _);
            Assert.Contains("this..ctor((x + 1))", text);
            Assert.Throws<NotSupportedException>(() => ctor.Decompile(new LinqDecompiler(), out _));
        }

        [Fact]
        public void CallSignature_DecodesStaticInstanceByRefPointerArrayAndVarArg()
        {
            // Static default: int (string, ref long, int*, byte[])
            var blob1 = new byte[] { 0x00, 0x04, 0x08, 0x0E, 0x10, 0x0A, 0x0F, 0x08, 0x1D, 0x05 };
            var sig1 = new CallSignature(blob1);
            Assert.False(sig1.HasThis);
            Assert.Equal(typeof(int), sig1.ReturnType);
            Assert.Equal(4, sig1.ParameterTypes.Length);
            Assert.Equal(typeof(string), sig1.ParameterTypes[0]);
            Assert.Equal(typeof(long).MakeByRefType(), sig1.ParameterTypes[1]);
            Assert.Equal(typeof(int).MakePointerType(), sig1.ParameterTypes[2]);
            Assert.Equal(typeof(byte[]), sig1.ParameterTypes[3]);
            Assert.Equal(-1, sig1.VarArgSentinel);

            // Instance varargs: void (bool, ..., double)
            var blob2 = new byte[] { 0x25, 0x02, 0x01, 0x02, 0x41, 0x0D };
            var sig2 = new CallSignature(blob2);
            Assert.True(sig2.HasThis);
            Assert.True((sig2.CallingConvention & System.Reflection.CallingConventions.VarArgs) != 0);
            Assert.Equal(typeof(void), sig2.ReturnType);
            Assert.Equal(2, sig2.ParameterTypes.Length);
            Assert.Equal(typeof(bool), sig2.ParameterTypes[0]);
            Assert.Equal(typeof(double), sig2.ParameterTypes[1]);
            Assert.Equal(1, sig2.VarArgSentinel);
        }

        [Fact]
        public void ILReader_ExposesMaxStackSizeAndInitLocals_AndLabelSupportsLargeOffsets()
        {
            var m = new Func<int, int>(ForSum).Method;
            var reader = m.GetILReader();
            Assert.True(reader.MaxStackSize > 0);
            Assert.True(reader.InitLocals);
            var lbl = new IL.Label { Offset = 100_000 };
            Assert.Equal(100_000, lbl.Offset);
        }

        static int AddTarget(int a, int b) => a + b;

        static unsafe int InvokeViaCalli(int a, int b)
        {
            delegate*<int, int, int> fn = &AddTarget;
            return fn(a, b);
        }

        static unsafe int PointerLoopSum(int* ptr, int count)
        {
            int sum = 0;
            int* end = ptr + count;
            for (int* p = ptr; p < end; p++)
                sum += *p;
            return sum;
        }

        [Fact]
        public void Calli_FunctionPointer_DecodesWithCustomVisitor()
        {
            var m = typeof(SemanticDecompilerTests).GetMethod(nameof(InvokeViaCalli), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var text = m.Decompile(new StringVisitor(), out _);
            Assert.Contains("&AddTarget", text);
            Assert.Contains("calli(", text);
            Assert.Contains("a, b", text);
            Assert.Throws<NotSupportedException>(() => m.Decompile(new LinqDecompiler(), out _));
        }

        [Fact]
        public void UnmanagedPointers_ArithmeticAndComparison_DecodesWithCustomVisitor()
        {
            var m = typeof(SemanticDecompilerTests).GetMethod(nameof(PointerLoopSum), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var text = m.Decompile(new StringVisitor(), out _);
            Assert.Contains("+", text);
            Assert.Contains("*", text);
            Assert.Throws<NotSupportedException>(() => m.Decompile(new LinqDecompiler(), out _));
        }

        class OpenGenericBox<T> where T : struct
        {
            public T Value;
            public string Describe() => Value.ToString();
        }

        [Fact]
        public void OpenGenericType_WithConstrainedStructParam_Decodes()
        {
            var m = typeof(OpenGenericBox<>).GetMethod(nameof(OpenGenericBox<int>.Describe));
            var text = m.Decompile(new StringVisitor(), out _);
            Assert.Contains("ToString", text);
        }

        #endregion
    }
}
