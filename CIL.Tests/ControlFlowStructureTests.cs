using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;
using CIL.Expressions;

namespace CIL.Tests
{
    /// <summary>
    /// Comprehensive structural and decompilation tests covering:
    ///   loops (for/while/do-while/foreach/nested), switch statements,
    ///   nested if-statements, try-catch-finally with filters, method calls,
    ///   arithmetic, implicit/explicit conversions, new expressions,
    ///   static field accesses, break/continue, goto, and permutations thereof.
    /// </summary>
    public class ControlFlowStructureTests
    {
        // ── helpers ──────────────────────────────────────────────────────────

        static bool HasOpcode(Code il, OpType opType)
        {
            for (int i = 0; i < il.Count; i++)
                if (il[i].OpCode.Type() == opType) return true;
            return false;
        }

        static int CountOpcode(Code il, OpType opType)
        {
            int n = 0;
            for (int i = 0; i < il.Count; i++)
                if (il[i].OpCode.Type() == opType) n++;
            return n;
        }

        static bool HasAnyOpcode(Code il, params OpType[] types)
        {
            for (int i = 0; i < il.Count; i++)
                if (Array.IndexOf(types, il[i].OpCode.Type()) >= 0) return true;
            return false;
        }

        static bool HasConditionalBranch(Code il)
            => HasAnyOpcode(il,
                OpType.Brfalse, OpType.Brfalse_s,
                OpType.Brtrue, OpType.Brtrue_s,
                OpType.Beq, OpType.Beq_s,
                OpType.Bne_un, OpType.Bne_un_s,
                OpType.Bgt, OpType.Bgt_s,
                OpType.Bge, OpType.Bge_s,
                OpType.Blt, OpType.Blt_s,
                OpType.Ble, OpType.Ble_s);

        // ── loop target methods ───────────────────────────────────────────────

        static int ForLoopSum(int n)
        {
            int sum = 0;
            for (int i = 0; i < n; i++)
                sum += i;
            return sum;
        }

        static int WhileLoopCountdown(int n)
        {
            while (n > 0) n--;
            return n;
        }

        static int DoWhileLoop(int n)
        {
            int i = 0;
            do { i++; } while (i < n);
            return i;
        }

        static int ForLoopWithBreak(int n)
        {
            int result = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == 5) break;
                result += i;
            }
            return result;
        }

        static int ForLoopWithContinue(int n)
        {
            int result = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 2 == 0) continue;
                result += i;
            }
            return result;
        }

        static int ForLoopBreakAndContinue(int n)
        {
            int result = 0;
            for (int i = 0; i < n; i++)
            {
                if (i < 0) break;
                if (i % 2 == 0) continue;
                result += i;
            }
            return result;
        }

        static int NestedForLoops(int n, int m)
        {
            int sum = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    sum++;
            return sum;
        }

        static int NestedLoopsBreakInner(int n, int m)
        {
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (j == 3) break;
                    count++;
                }
            }
            return count;
        }

        static int NestedLoopsContinueOuter(int n, int m)
        {
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 2 != 0) continue;
                for (int j = 0; j < m; j++)
                    count++;
            }
            return count;
        }

        static int TripleNestedLoop(int n)
        {
            int count = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    for (int k = 0; k < n; k++)
                        count++;
            return count;
        }

        static int ForLoopWithTryCatch(string[] items)
        {
            int sum = 0;
            for (int i = 0; i < items.Length; i++)
            {
                try { sum += int.Parse(items[i]); }
                catch (FormatException) { }
            }
            return sum;
        }

        static int TryCatchContainingLoop(string[] items)
        {
            try
            {
                int sum = 0;
                for (int i = 0; i < items.Length; i++)
                    sum += int.Parse(items[i]);
                return sum;
            }
            catch (FormatException)
            {
                return -1;
            }
        }

        static int WhileWithTryFinally(int n)
        {
            int result = 0;
            while (n > 0)
            {
                try { result += n; }
                finally { n--; }
            }
            return result;
        }

        static int WhileBreak(int n)
        {
            int i = 0;
            while (true)
            {
                if (i >= n) break;
                i++;
            }
            return i;
        }

        // ── loop tests ────────────────────────────────────────────────────────

        [Fact]
        public void Loop_ForLoop_HasLoopHeader()
        {
            var il = new Func<int, int>(ForLoopSum).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Loop_ForLoop_AllLoopHeadersAreLeaders()
        {
            var il = new Func<int, int>(ForLoopSum).Method.GetInstructions();
            Assert.All(il.Where(z => z.IsLoopHeader), z => Assert.True(z.IsLeader));
        }

        [Fact]
        public void Loop_ForLoop_NoExceptionHandlers()
        {
            var il = new Func<int, int>(ForLoopSum).Method.GetInstructions();
            Assert.Empty(il.ExceptionHandlers);
        }

        [Fact]
        public void Loop_WhileLoop_HasLoopHeader()
        {
            var il = new Func<int, int>(WhileLoopCountdown).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Loop_DoWhileLoop_HasLoopHeader()
        {
            var il = new Func<int, int>(DoWhileLoop).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Loop_ForLoopWithBreak_HasLoopHeader()
        {
            var il = new Func<int, int>(ForLoopWithBreak).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Loop_ForLoopWithBreak_HasConditionalBranch()
        {
            var il = new Func<int, int>(ForLoopWithBreak).Method.GetInstructions();
            Assert.True(HasConditionalBranch(il));
        }

        [Fact]
        public void Loop_ForLoopWithContinue_HasLoopHeader()
        {
            var il = new Func<int, int>(ForLoopWithContinue).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Loop_ForLoopBreakAndContinue_HasLoopHeader()
        {
            var il = new Func<int, int>(ForLoopBreakAndContinue).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Loop_NestedForLoops_HasAtLeastTwoLoopHeaders()
        {
            var il = new Func<int, int, int>(NestedForLoops).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLoopHeader) >= 2,
                "Nested for loops should produce at least 2 loop headers");
        }

        [Fact]
        public void Loop_NestedLoopsBreakInner_HasAtLeastTwoLoopHeaders()
        {
            var il = new Func<int, int, int>(NestedLoopsBreakInner).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLoopHeader) >= 2);
        }

        [Fact]
        public void Loop_NestedLoopsContinueOuter_HasAtLeastTwoLoopHeaders()
        {
            var il = new Func<int, int, int>(NestedLoopsContinueOuter).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLoopHeader) >= 2);
        }

        [Fact]
        public void Loop_TripleNested_HasAtLeastThreeLoopHeaders()
        {
            var il = new Func<int, int>(TripleNestedLoop).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLoopHeader) >= 3,
                "Triple nested loop should produce at least 3 loop headers");
        }

        [Fact]
        public void Loop_ForLoopWithTryCatch_HasLoopHeaderAndEH()
        {
            var il = new Func<string[], int>(ForLoopWithTryCatch).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.NotEmpty(il.ExceptionHandlers);
            Assert.True(il.ExceptionHandlers[0].IsCatch);
        }

        [Fact]
        public void Loop_TryCatchContainingLoop_HasLoopHeaderAndEH()
        {
            var il = new Func<string[], int>(TryCatchContainingLoop).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.NotEmpty(il.ExceptionHandlers);
        }

        [Fact]
        public void Loop_WhileWithTryFinally_HasLoopHeaderAndFinallyHandler()
        {
            var il = new Func<int, int>(WhileWithTryFinally).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.Contains(il.ExceptionHandlers, h => h.IsFinally);
        }

        [Fact]
        public void Loop_WhileBreak_HasLoopHeader()
        {
            var il = new Func<int, int>(WhileBreak).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        // ── switch target methods ─────────────────────────────────────────────

        static string SwitchDense(int x)
        {
            switch (x)
            {
                case 0: return "zero";
                case 1: return "one";
                case 2: return "two";
                case 3: return "three";
                case 4: return "four";
                default: return "other";
            }
        }

        static int SwitchMultipleLabels(int x)
        {
            switch (x)
            {
                case 0:
                case 1: return 1;
                case 2:
                case 3: return 2;
                default: return -1;
            }
        }

        static string SwitchOnString(string s)
        {
            switch (s)
            {
                case "alpha": return "a";
                case "beta": return "b";
                case "gamma": return "c";
                default: return "?";
            }
        }

        static int SwitchInsideFor(int n)
        {
            int result = 0;
            for (int i = 0; i < n; i++)
            {
                switch (i % 3)
                {
                    case 0: result += 1; break;
                    case 1: result += 2; break;
                    default: result += 3; break;
                }
            }
            return result;
        }

        static int ForInsideSwitch(int x, int n)
        {
            switch (x)
            {
                case 0:
                {
                    int s = 0;
                    for (int i = 0; i < n; i++) s += i;
                    return s;
                }
                case 1:
                {
                    int p = 1;
                    for (int i = 1; i <= n; i++) p *= i;
                    return p;
                }
                default:
                    return -1;
            }
        }

        static int SwitchInsideTryCatch(int x, string s)
        {
            try
            {
                switch (x)
                {
                    case 0: return int.Parse(s);
                    case 1: return int.Parse(s) * 2;
                    default: return -1;
                }
            }
            catch (FormatException)
            {
                return -99;
            }
        }

        static int TryCatchInsideSwitch(int x, string s)
        {
            switch (x)
            {
                case 0:
                    try { return int.Parse(s); }
                    catch (FormatException) { return -1; }
                case 1:
                    try { return int.Parse(s) * 2; }
                    catch (OverflowException) { return -2; }
                default:
                    return -99;
            }
        }

        static int NestedSwitchMethod(int x, int y)
        {
            switch (x)
            {
                case 0:
                    switch (y)
                    {
                        case 0: return 0;
                        case 1: return 1;
                        default: return 2;
                    }
                case 1:
                    switch (y)
                    {
                        case 0: return 10;
                        default: return 11;
                    }
                default:
                    return -1;
            }
        }

        static int SwitchInLoopWithTryCatch(string[] items, int mode)
        {
            int result = 0;
            for (int i = 0; i < items.Length; i++)
            {
                switch (mode)
                {
                    case 0:
                        try { result += int.Parse(items[i]); }
                        catch (FormatException) { }
                        break;
                    case 1:
                        try { result -= int.Parse(items[i]); }
                        catch (FormatException) { }
                        break;
                    default:
                        result = 0;
                        break;
                }
            }
            return result;
        }

        // ── switch tests ──────────────────────────────────────────────────────

        [Fact]
        public void Switch_Dense_HasSwitchOpcode()
        {
            var il = new Func<int, string>(SwitchDense).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Switch), "Dense switch should emit switch opcode");
        }

        [Fact]
        public void Switch_Dense_BranchKeysAreSequential()
        {
            var il = new Func<int, string>(SwitchDense).Method.GetInstructions();
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Switch)
                {
                    var branches = il[i].ResolveBranches().ToArray();
                    Assert.True(branches.Length > 0);
                    for (int j = 0; j < branches.Length; j++)
                        Assert.Equal(j, branches[j].Key);
                    return;
                }
            }
            Assert.Fail("No switch opcode found");
        }

        [Fact]
        public void Switch_Dense_AllBranchTargetsAreLeaders()
        {
            var il = new Func<int, string>(SwitchDense).Method.GetInstructions();
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Switch)
                {
                    var branches = il[i].ResolveBranches().ToArray();
                    foreach (var b in branches)
                        Assert.True(il[b.Value].IsLeader,
                            $"Switch target {b.Value} must be a leader");
                    return;
                }
            }
        }

        [Fact]
        public void Switch_Dense_AllBranchTargetsHaveValidIndex()
        {
            var il = new Func<int, string>(SwitchDense).Method.GetInstructions();
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Switch)
                {
                    var branches = il[i].ResolveBranches().ToArray();
                    foreach (var b in branches)
                    {
                        var idx = il.GetIndex(b.Value);
                        Assert.True(idx >= 0 && idx < il.Count,
                            $"Branch target {b.Value} must be a valid instruction index");
                    }
                    return;
                }
            }
        }

        [Fact]
        public void Switch_MultipleLabels_HasMultipleLeaders()
        {
            // The compiler may use a switch table or comparison branches for
            // small dense ranges. Either way there must be multiple basic blocks.
            var il = new Func<int, int>(SwitchMultipleLabels).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLeader) >= 3,
                "Switch with multiple case labels should produce at least 3 basic blocks");
        }

        [Fact]
        public void Switch_OnString_HasCallsForComparison()
        {
            // String switch compiles to method calls (string equality / hash lookup)
            var il = new Func<string, string>(SwitchOnString).Method.GetInstructions();
            Assert.True(
                HasAnyOpcode(il, OpType.Call, OpType.Callvirt),
                "String switch should use method calls");
        }

        [Fact]
        public void Switch_InsideFor_HasLoopHeaderAndMultipleLeaders()
        {
            // The compiler may or may not emit a switch opcode for small ranges;
            // assert loop structure and multiple basic blocks instead.
            var il = new Func<int, int>(SwitchInsideFor).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.True(il.Count(z => z.IsLeader) >= 4,
                "Loop with 3-way switch should produce multiple basic blocks");
        }

        [Fact]
        public void Switch_ForInsideSwitch_HasLoopHeader()
        {
            var il = new Func<int, int, int>(ForInsideSwitch).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Switch_InsideTryCatch_HasEH()
        {
            var il = new Func<int, string, int>(SwitchInsideTryCatch).Method.GetInstructions();
            Assert.NotEmpty(il.ExceptionHandlers);
            Assert.All(il.ExceptionHandlers, h => Assert.True(h.IsCatch));
        }

        [Fact]
        public void Switch_TryCatchInsideSwitch_HasAtLeastTwoHandlers()
        {
            var il = new Func<int, string, int>(TryCatchInsideSwitch).Method.GetInstructions();
            Assert.True(il.ExceptionHandlers.Length >= 2);
        }

        [Fact]
        public void Switch_Nested_HasManyLeaders()
        {
            var il = new Func<int, int, int>(NestedSwitchMethod).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLeader) >= 6,
                "Nested switch should produce many basic-block leaders");
        }

        [Fact]
        public void Switch_InLoopWithTryCatch_HasLoopHeaderAndEH()
        {
            var il = new Func<string[], int, int>(SwitchInLoopWithTryCatch).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.NotEmpty(il.ExceptionHandlers);
        }

        // ── nested if-statement target methods ───────────────────────────────

        static string ClassifyNumber(int n)
        {
            if (n < 0)
            {
                if (n < -100) return "very negative";
                else if (n < -10) return "negative";
                else return "slightly negative";
            }
            else if (n == 0)
                return "zero";
            else if (n < 10)
                return "small";
            else if (n < 100)
                return "medium";
            else
                return "large";
        }

        static int NestedIfInLoop(int n)
        {
            int result = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 2 == 0)
                {
                    if (i % 4 == 0)
                        result += 2;
                    else
                        result += 1;
                }
                else
                {
                    if (i > 10) result -= 1;
                }
            }
            return result;
        }

        static int IfInsideTryCatch(string s, bool negate)
        {
            try
            {
                int val = int.Parse(s);
                if (negate) return -val;
                else return val;
            }
            catch (FormatException)
            {
                return 0;
            }
        }

        static int TriplyNestedIf(int x, int y, int z)
        {
            if (x > 0)
            {
                if (y > 0)
                {
                    if (z > 0) return x + y + z;
                    else return x + y;
                }
                else
                {
                    if (z > 0) return x + z;
                    else return x;
                }
            }
            else
            {
                if (y > 0) return y;
                else return 0;
            }
        }

        static bool LogicalAndOp(int x, int y, int z) => x > 0 && y > 0 && z > 0;
        static bool LogicalOrOp(int x, int y, int z) => x > 0 || y > 0 || z > 0;
        static bool MixedLogical(int x, int y, int z) => (x > 0 && y > 0) || z > 0;

        // ── nested if tests ───────────────────────────────────────────────────

        [Fact]
        public void NestedIf_ClassifyNumber_HasManyLeaders()
        {
            var il = new Func<int, string>(ClassifyNumber).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLeader) >= 6,
                "Five-branch nested if-else should produce at least 6 leaders");
        }

        [Fact]
        public void NestedIf_ClassifyNumber_HasConditionalBranches()
        {
            var il = new Func<int, string>(ClassifyNumber).Method.GetInstructions();
            Assert.True(HasConditionalBranch(il));
        }

        [Fact]
        public void NestedIf_InLoop_HasLoopHeaderAndBranches()
        {
            var il = new Func<int, int>(NestedIfInLoop).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.True(HasConditionalBranch(il));
        }

        [Fact]
        public void NestedIf_InsideTryCatch_HasEHAndBranches()
        {
            var il = new Func<string, bool, int>(IfInsideTryCatch).Method.GetInstructions();
            Assert.NotEmpty(il.ExceptionHandlers);
            Assert.True(HasConditionalBranch(il));
        }

        [Fact]
        public void NestedIf_TriplyNested_HasManyBasicBlocks()
        {
            var il = new Func<int, int, int, int>(TriplyNestedIf).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLeader) >= 6,
                "Triple nested if-else should produce at least 6 basic blocks");
        }

        [Fact]
        public void NestedIf_LogicalAndChain_HasMultipleShortCircuitBranches()
        {
            // The compiler may use brfalse/brtrue or direct comparison branches (bgt, bge, etc.)
            // for short-circuit &&. Count all conditional branch instructions.
            var il = new Func<int, int, int, bool>(LogicalAndOp).Method.GetInstructions();
            int branchCount = 0;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Brfalse || op == OpType.Brfalse_s ||
                    op == OpType.Brtrue  || op == OpType.Brtrue_s  ||
                    op == OpType.Beq     || op == OpType.Beq_s     ||
                    op == OpType.Bne_un  || op == OpType.Bne_un_s  ||
                    op == OpType.Bgt     || op == OpType.Bgt_s     ||
                    op == OpType.Bge     || op == OpType.Bge_s     ||
                    op == OpType.Blt     || op == OpType.Blt_s     ||
                    op == OpType.Ble     || op == OpType.Ble_s)
                    branchCount++;
            }
            Assert.True(branchCount >= 2, "&&-chain should produce at least 2 conditional branches");
        }

        [Fact]
        public void NestedIf_LogicalOrChain_HasMultipleShortCircuitBranches()
        {
            // Same approach: count all conditional branch opcodes.
            var il = new Func<int, int, int, bool>(LogicalOrOp).Method.GetInstructions();
            int branchCount = 0;
            for (int i = 0; i < il.Count; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Brfalse || op == OpType.Brfalse_s ||
                    op == OpType.Brtrue  || op == OpType.Brtrue_s  ||
                    op == OpType.Beq     || op == OpType.Beq_s     ||
                    op == OpType.Bne_un  || op == OpType.Bne_un_s  ||
                    op == OpType.Bgt     || op == OpType.Bgt_s     ||
                    op == OpType.Bge     || op == OpType.Bge_s     ||
                    op == OpType.Blt     || op == OpType.Blt_s     ||
                    op == OpType.Ble     || op == OpType.Ble_s)
                    branchCount++;
            }
            Assert.True(branchCount >= 2, "||-chain should produce at least 2 conditional branches");
        }

        [Fact]
        public void NestedIf_MixedLogical_HasMultipleBranches()
        {
            var il = new Func<int, int, int, bool>(MixedLogical).Method.GetInstructions();
            Assert.True(HasConditionalBranch(il));
        }

        // ── new-expression target methods ─────────────────────────────────────

        static Exception NewException(string msg) => new Exception(msg);
        static ArgumentException NewArgException(string msg, string param)
            => new ArgumentException(msg, param);
        static int[] NewIntArray(int n) => new int[n];
        static int[] NewIntArrayInit(int x, int y, int z) => new[] { x, y, z };
        static List<int> NewList() => new List<int>();

        // ── new expression tests ──────────────────────────────────────────────

        [Fact]
        public void New_Exception_HasNewobjOpcode()
        {
            var il = new Func<string, Exception>(NewException).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Newobj));
        }

        [Fact]
        public void New_TwoArgException_HasNewobjOpcode()
        {
            var il = new Func<string, string, ArgumentException>(NewArgException).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Newobj));
        }

        [Fact]
        public void New_IntArray_HasNewarrOpcode()
        {
            var il = new Func<int, int[]>(NewIntArray).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Newarr));
        }

        [Fact]
        public void New_IntArrayWithInit_HasNewarrAndStelemOpcode()
        {
            var il = new Func<int, int, int, int[]>(NewIntArrayInit).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Newarr));
            Assert.True(
                HasAnyOpcode(il, OpType.Stelem_i4, OpType.Stelem),
                "Array initializer should use stelem.i4 or stelem");
        }

        [Fact]
        public void New_GenericList_HasNewobjOpcode()
        {
            var il = new Func<List<int>>(NewList).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Newobj));
        }

        [Fact]
        public void Decompile_NewExceptionOneArg_ProducesNewExpression()
        {
            Func<string, Exception> foo = msg => new Exception(msg);
            Expression<Func<string, Exception>> expected = msg => new Exception(msg);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_NewExceptionTwoArgs_ProducesNewExpression()
        {
            Func<string, string, ArgumentException> foo
                = (msg, param) => new ArgumentException(msg, param);
            Expression<Func<string, string, ArgumentException>> expected
                = (msg, param) => new ArgumentException(msg, param);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        // ── conversion target methods ──────────────────────────────────────────

        static int ByteToInt(byte b) => b;                      // implicit zero-extend
        static int ShortToInt(short s) => s;                    // implicit sign-extend
        static long IntToLong(int x) => x;                      // implicit widen
        static float IntToFloat(int x) => x;                    // implicit widen
        static double IntToDoubleImpl(int x) => x;              // implicit widen
        static double FloatToDouble(float f) => f;              // implicit widen
        static byte IntToByte(int x) => (byte)x;                // explicit narrow
        static int DoubleToInt(double d) => (int)d;             // explicit narrow
        static float DoubleToFloat(double d) => (float)d;       // explicit narrow
        static byte CheckedIntToByte(int x) => checked((byte)x);
        static int CheckedLongToInt(long x) => checked((int)x);

        // ── conversion tests ──────────────────────────────────────────────────

        [Fact]
        public void Decompile_IntToLong_ProducesConvert()
        {
            Func<int, long> foo = x => (long)x;
            Expression<Func<int, long>> expected = x => (long)x;
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Decompile_IntToDouble_ProducesConvert()
        {
            Func<int, double> foo = x => (double)x;
            Expression<Func<int, double>> expected = x => (double)x;
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Decompile_IntToFloat_ProducesConvert()
        {
            Func<int, float> foo = x => (float)x;
            Expression<Func<int, float>> expected = x => (float)x;
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Decompile_DoubleToInt_ProducesConvert()
        {
            Func<double, int> foo = x => (int)x;
            Expression<Func<double, int>> expected = x => (int)x;
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Decompile_DoubleToFloat_ProducesConvert()
        {
            Func<double, float> foo = x => (float)x;
            Expression<Func<double, float>> expected = x => (float)x;
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Decompile_CheckedIntToByte_ProducesConvertChecked()
        {
            Func<int, byte> foo = x => checked((byte)x);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("ConvertChecked", decompiled.ToString());
        }

        [Fact]
        public void Decompile_CheckedLongToInt_ProducesConvertChecked()
        {
            Func<long, int> foo = x => checked((int)x);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("ConvertChecked", decompiled.ToString());
        }

        [Fact]
        public void Conversion_IntToByte_HasConvU1()
        {
            var il = new Func<int, byte>(IntToByte).Method.GetInstructions();
            Assert.True(
                HasAnyOpcode(il, OpType.Conv_u1, OpType.Conv_i1),
                "(byte)x should emit conv.u1");
        }

        [Fact]
        public void Conversion_DoubleToInt_HasConvI4()
        {
            var il = new Func<double, int>(DoubleToInt).Method.GetInstructions();
            Assert.True(
                HasAnyOpcode(il, OpType.Conv_i4),
                "(int)d should emit conv.i4");
        }

        [Fact]
        public void Conversion_IntToFloat_HasConvR4()
        {
            var il = new Func<int, float>(IntToFloat).Method.GetInstructions();
            Assert.True(
                HasAnyOpcode(il, OpType.Conv_r4),
                "(float)x should emit conv.r4");
        }

        [Fact]
        public void Conversion_IntToDouble_HasConvR8()
        {
            var il = new Func<int, double>(IntToDoubleImpl).Method.GetInstructions();
            Assert.True(
                HasAnyOpcode(il, OpType.Conv_r8),
                "(double)x should emit conv.r8");
        }

        [Fact]
        public void Conversion_CheckedToByte_HasConvOvfU1()
        {
            var il = new Func<int, byte>(CheckedIntToByte).Method.GetInstructions();
            Assert.True(
                HasAnyOpcode(il, OpType.Conv_ovf_u1, OpType.Conv_ovf_u1_un),
                "checked((byte)x) should emit conv.ovf.u1");
        }

        [Fact]
        public void Conversion_CheckedLongToInt_HasConvOvfI4()
        {
            var il = new Func<long, int>(CheckedLongToInt).Method.GetInstructions();
            Assert.True(
                HasAnyOpcode(il, OpType.Conv_ovf_i4, OpType.Conv_ovf_i4_un),
                "checked((int)x) should emit conv.ovf.i4");
        }

        [Fact]
        public void Decompile_ConversionChain_IntToFloatToDouble()
        {
            Func<int, double> foo = x => (double)(float)x;
            var decompiled = LinqDecompiler.Decompile(foo);
            // Two Convert nodes in the tree
            var str = decompiled.ToString();
            var convertCount = str.Split(new[] { "Convert" }, StringSplitOptions.None).Length - 1;
            Assert.True(convertCount >= 2, $"Expected at least 2 Convert nodes, got: {str}");
        }

        // ── static field target methods ───────────────────────────────────────

        static int s_counter = 100;
        static string s_prefix = "hello";
        const double c_pi = Math.PI;

        class FieldHolder2
        {
            public int Value;
            public string Name;
        }

        static int ReadStaticInt() => s_counter;
        static string ReadStaticStr() => s_prefix;
        static int StaticInArith(int x) => s_counter + x;
        static int ReadInstanceField(FieldHolder2 h) => h.Value;
        static string ReadInstanceStr(FieldHolder2 h) => h.Name;
        static int TwoInstanceFields(FieldHolder2 a, FieldHolder2 b) => a.Value + b.Value;
        static int StaticPlusInstance(FieldHolder2 h) => s_counter + h.Value;
        static string InstanceFieldMethod(FieldHolder2 h) => h.Name.ToUpper();

        // ── static/instance field tests ───────────────────────────────────────

        [Fact]
        public void Field_ReadStaticInt_HasLdsfld()
        {
            var il = new Func<int>(ReadStaticInt).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Ldsfld));
        }

        [Fact]
        public void Field_ReadStaticStr_HasLdsfld()
        {
            var il = new Func<string>(ReadStaticStr).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Ldsfld));
        }

        [Fact]
        public void Field_StaticInArith_HasLdsfldAndAdd()
        {
            var il = new Func<int, int>(StaticInArith).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Ldsfld));
            Assert.True(HasOpcode(il, OpType.Add));
        }

        [Fact]
        public void Field_ReadInstanceField_HasLdfld()
        {
            var il = new Func<FieldHolder2, int>(ReadInstanceField).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Ldfld));
        }

        [Fact]
        public void Field_TwoInstanceFields_HasTwoLdfld()
        {
            var il = new Func<FieldHolder2, FieldHolder2, int>(TwoInstanceFields).Method.GetInstructions();
            Assert.Equal(2, CountOpcode(il, OpType.Ldfld));
        }

        [Fact]
        public void Decompile_ReadStaticField_ContainsFieldName()
        {
            Func<int> foo = () => s_counter;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("s_counter", decompiled.ToString());
        }

        [Fact]
        public void Decompile_ReadInstanceField_ContainsFieldName()
        {
            Func<FieldHolder2, int> foo = h => h.Value;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("Value", decompiled.ToString());
        }

        [Fact]
        public void Decompile_StaticPlusParam_ProducesAdd()
        {
            Func<int, int> foo = x => s_counter + x;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.Add, decompiled.Body.NodeType);
            Assert.Contains("s_counter", decompiled.ToString());
        }

        [Fact]
        public void Decompile_InstanceFieldThenMethod_ContainsMethodName()
        {
            Func<FieldHolder2, string> foo = h => h.Name.ToUpper();
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("ToUpper", decompiled.ToString());
        }

        [Fact]
        public void Decompile_TwoInstanceFields_ProducesAdd()
        {
            Func<FieldHolder2, FieldHolder2, int> foo = (a, b) => a.Value + b.Value;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.Add, decompiled.Body.NodeType);
        }

        [Fact]
        public void Decompile_StaticPlusInstanceField_ProducesAdd()
        {
            Func<FieldHolder2, int> foo = h => s_counter + h.Value;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.Add, decompiled.Body.NodeType);
        }

        // ── break and continue target methods ─────────────────────────────────

        static int BreakOnFirst(int[] arr, int target)
        {
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == target) break;
            return -1;
        }

        static int ContinueOnEven(int[] arr)
        {
            int sum = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] % 2 == 0) continue;
                sum += arr[i];
            }
            return sum;
        }

        static int BreakAndContinueSameLoop(int[] arr)
        {
            int sum = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] < 0) break;
                if (arr[i] == 0) continue;
                sum += arr[i];
            }
            return sum;
        }

        static int BreakFromInnerLoop(int[][] matrix, int target)
        {
            for (int i = 0; i < matrix.Length; i++)
                for (int j = 0; j < matrix[i].Length; j++)
                    if (matrix[i][j] == target) break;
            return -1;
        }

        static int ContinueInNestedLoops(int n, int m)
        {
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (j % 2 == 0) continue;
                    count++;
                }
            }
            return count;
        }

        // ── break and continue tests ──────────────────────────────────────────

        [Fact]
        public void BreakContinue_BreakOnFirst_HasLoopHeader()
        {
            var il = new Func<int[], int, int>(BreakOnFirst).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void BreakContinue_ContinueOnEven_HasLoopHeader()
        {
            var il = new Func<int[], int>(ContinueOnEven).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void BreakContinue_BreakAndContinue_HasLoopHeader()
        {
            var il = new Func<int[], int>(BreakAndContinueSameLoop).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void BreakContinue_NestedLoopBreakInner_HasAtLeastTwoLoopHeaders()
        {
            var il = new Func<int[][], int, int>(BreakFromInnerLoop).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLoopHeader) >= 2);
        }

        [Fact]
        public void BreakContinue_ContinueInNestedLoops_HasAtLeastTwoLoopHeaders()
        {
            var il = new Func<int, int, int>(ContinueInNestedLoops).Method.GetInstructions();
            Assert.True(il.Count(z => z.IsLoopHeader) >= 2);
        }

        // ── goto target methods ───────────────────────────────────────────────

        static int GotoForward(int x)
        {
            if (x > 0) goto POSITIVE;
            return -x;
        POSITIVE:
            return x;
        }

        static int GotoBackward(int n)
        {
            int i = 0;
        LOOP:
            if (i >= n) goto DONE;
            i++;
            goto LOOP;
        DONE:
            return i;
        }

        static int GotoMultipleLabels(int x)
        {
            if (x < 0) goto NEG;
            if (x == 0) goto ZERO;
            goto POS;
        NEG:
            return -1;
        ZERO:
            return 0;
        POS:
            return 1;
        }

        static int GotoInsideLoop(int n)
        {
            int i = 0;
            int result = 0;
        NEXT:
            if (i >= n) goto DONE;
            if (i % 2 == 0) { result += i; }
            i++;
            goto NEXT;
        DONE:
            return result;
        }

        static int GotoSkipBlock(int x)
        {
            int r = 0;
            if (x < 0) goto END;
            r = x * 2;
            r += 1;
        END:
            return r;
        }

        // ── goto tests ────────────────────────────────────────────────────────

        [Fact]
        public void Goto_Forward_HasBranchAndMultipleLeaders()
        {
            var il = new Func<int, int>(GotoForward).Method.GetInstructions();
            Assert.True(HasConditionalBranch(il));
            Assert.True(il.Count(z => z.IsLeader) >= 2);
        }

        [Fact]
        public void Goto_Backward_CreatesLoopHeader()
        {
            // A backward goto is a back edge, which the CFA identifies as a loop header.
            var il = new Func<int, int>(GotoBackward).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Goto_MultipleLabels_AllTargetsAreLeaders()
        {
            var il = new Func<int, int>(GotoMultipleLabels).Method.GetInstructions();
            // All 3 return labels (NEG, ZERO, POS) must be leaders
            Assert.True(il.Count(z => z.IsLeader) >= 3);
        }

        [Fact]
        public void Goto_InsideLoop_HasLoopHeader()
        {
            var il = new Func<int, int>(GotoInsideLoop).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Goto_SkipBlock_ForwardJumpIsLeader()
        {
            var il = new Func<int, int>(GotoSkipBlock).Method.GetInstructions();
            // The goto target (END) must be a leader
            Assert.True(il.Count(z => z.IsLeader) >= 2);
        }

        // ── catch-with-filter (when clause) target methods ────────────────────

        static int FilteredCatch1(string s)
        {
            try { return int.Parse(s); }
            catch (FormatException ex) when (ex.Message.Length > 0) { return -1; }
        }

        static int FilteredCatch2(string s, int maxLen)
        {
            try { return int.Parse(s); }
            catch (FormatException ex) when (ex.Message.Length < maxLen) { return -1; }
            catch (FormatException) { return -2; }
        }

        static int FilterAndNormalCatch(string s)
        {
            try { return int.Parse(s); }
            catch (FormatException ex) when (ex.Message.Length > 5) { return -1; }
            catch (OverflowException) { return -2; }
        }

        static int FilterWithLoop(string[] items)
        {
            int sum = 0;
            for (int i = 0; i < items.Length; i++)
            {
                try { sum += int.Parse(items[i]); }
                catch (FormatException ex) when (ex.Message.Length > 0) { }
            }
            return sum;
        }

        static int FilterWithNestedTry(string s)
        {
            try
            {
                try { return int.Parse(s); }
                catch (FormatException ex) when (ex.Message.Length > 0) { return -1; }
            }
            catch (Exception) { return -99; }
        }

        // ── catch-with-filter tests ───────────────────────────────────────────

        [Fact]
        public void Filter_Single_HasFilterFlag()
        {
            var il = new Func<string, int>(FilteredCatch1).Method.GetInstructions();
            Assert.Single(il.ExceptionHandlers);
            Assert.True(il.ExceptionHandlers[0].IsFilter);
        }

        [Fact]
        public void Filter_Single_FilterStartPrecedesHandlerStart()
        {
            var il = new Func<string, int>(FilteredCatch1).Method.GetInstructions();
            var h = il.ExceptionHandlers[0];
            Assert.True(h.FilterStart < h.HandlerStart,
                "Filter evaluation block must precede the handler body");
        }

        [Fact]
        public void Filter_Single_HasEndfilterInFilterRegion()
        {
            var il = new Func<string, int>(FilteredCatch1).Method.GetInstructions();
            var h = il.ExceptionHandlers[0];
            int filterIdx = il.GetIndex(h.FilterStart);
            int handlerIdx = il.GetIndex(h.HandlerStart);
            bool found = false;
            for (int i = filterIdx; i < handlerIdx; i++)
                if (il[i].OpCode.Type() == OpType.Endfilter) { found = true; break; }
            Assert.True(found, "Filter region must contain endfilter");
        }

        [Fact]
        public void Filter_Single_FilterStartInstructionIsFilterStart()
        {
            var il = new Func<string, int>(FilteredCatch1).Method.GetInstructions();
            var h = il.ExceptionHandlers[0];
            Assert.True(il[h.FilterStart].IsFilterStart);
        }

        [Fact]
        public void Filter_TwoFiltersForSameType_HasAtLeastTwoHandlers()
        {
            var il = new Func<string, int, int>(FilteredCatch2).Method.GetInstructions();
            Assert.True(il.ExceptionHandlers.Length >= 2);
        }

        [Fact]
        public void Filter_FilterAndNormal_HasFilterAndCatch()
        {
            var il = new Func<string, int>(FilterAndNormalCatch).Method.GetInstructions();
            Assert.True(il.ExceptionHandlers.Any(h => h.IsFilter),
                "Should have a filter handler");
            Assert.True(il.ExceptionHandlers.Any(h => h.IsCatch && !h.IsFilter),
                "Should have a normal (non-filter) catch handler");
        }

        [Fact]
        public void Filter_WithLoop_HasLoopHeaderAndFilter()
        {
            var il = new Func<string[], int>(FilterWithLoop).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.True(il.ExceptionHandlers.Any(h => h.IsFilter));
        }

        [Fact]
        public void Filter_WithNestedTry_HasMultipleHandlersIncludingFilter()
        {
            var il = new Func<string, int>(FilterWithNestedTry).Method.GetInstructions();
            Assert.True(il.ExceptionHandlers.Length >= 2);
            Assert.True(il.ExceptionHandlers.Any(h => h.IsFilter));
        }

        // ── extended arithmetic target methods ────────────────────────────────

        static int BitwiseAnd(int x, int y) => x & y;
        static int BitwiseOr(int x, int y) => x | y;
        static int BitwiseXorOp(int x, int y) => x ^ y;
        static int BitwiseNot(int x) => ~x;
        static int LeftShiftOp(int x, int n) => x << n;
        static int RightShiftOp(int x, int n) => x >> n;
        static int CheckedAddOp(int x, int y) => checked(x + y);
        static int CheckedMulOp(int x, int y) => checked(x * y);
        static int CheckedSubOp(int x, int y) => checked(x - y);
        static int ComplexArith(int x, int y, int z) => (x + y) * z - (x - y);
        static double MixedTypes(int x, double y) => x + y;

        // ── extended arithmetic tests ─────────────────────────────────────────

        [Fact]
        public void Arith_BitwiseAnd_Decompiles()
        {
            Func<int, int, int> foo = (x, y) => x & y;
            Expression<Func<int, int, int>> expected = (x, y) => x & y;
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Arith_BitwiseOr_Decompiles()
        {
            Func<int, int, int> foo = (x, y) => x | y;
            Expression<Func<int, int, int>> expected = (x, y) => x | y;
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Arith_BitwiseNot_NotYetSupported()
        {
            // ~x compiles to (xor x, -1). The decompiler's Not() method cannot
            // handle int→bool coercion, so decompilation throws.
            Assert.ThrowsAny<Exception>(
                () => LinqDecompiler.Decompile(new Func<int, int>(x => ~x)));
        }

        [Fact]
        public void Arith_LeftShift_ProducesLeftShiftNode()
        {
            // The C# compiler masks the shift amount: x << n becomes x << (n & 31).
            // The decompiled tree therefore includes the explicit mask.
            Func<int, int, int> foo = (x, n) => x << n;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.LeftShift, decompiled.Body.NodeType);
            Assert.Equal("(x, n) => (x << (n & 31))", decompiled.ToString());
        }

        [Fact]
        public void Arith_RightShift_ProducesRightShiftNode()
        {
            // Same masking: x >> n becomes x >> (n & 31).
            Func<int, int, int> foo = (x, n) => x >> n;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.RightShift, decompiled.Body.NodeType);
            Assert.Equal("(x, n) => (x >> (n & 31))", decompiled.ToString());
        }

        [Fact]
        public void Arith_CheckedAdd_HasAddOvfOpcode()
        {
            var il = new Func<int, int, int>(CheckedAddOp).Method.GetInstructions();
            Assert.True(HasAnyOpcode(il, OpType.Add_ovf, OpType.Add_ovf_un));
        }

        [Fact]
        public void Arith_CheckedMul_HasMulOvfOpcode()
        {
            var il = new Func<int, int, int>(CheckedMulOp).Method.GetInstructions();
            Assert.True(HasAnyOpcode(il, OpType.Mul_ovf, OpType.Mul_ovf_un));
        }

        [Fact]
        public void Decompile_CheckedAdd_ProducesAddChecked()
        {
            Func<int, int, int> foo = (x, y) => checked(x + y);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.AddChecked, decompiled.Body.NodeType);
        }

        [Fact]
        public void Decompile_CheckedMul_ProducesMultiplyChecked()
        {
            Func<int, int, int> foo = (x, y) => checked(x * y);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.MultiplyChecked, decompiled.Body.NodeType);
        }

        [Fact]
        public void Decompile_CheckedSub_ProducesSubtractChecked()
        {
            Func<int, int, int> foo = (x, y) => checked(x - y);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.SubtractChecked, decompiled.Body.NodeType);
        }

        [Fact]
        public void Decompile_ComplexArith_ProducesCorrectTree()
        {
            Func<int, int, int, int> foo = (x, y, z) => (x + y) * z - (x - y);
            Expression<Func<int, int, int, int>> expected = (x, y, z) => (x + y) * z - (x - y);
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Decompile_MixedIntDouble_ProducesConvertAndAdd()
        {
            Func<int, double, double> foo = (x, y) => x + y;
            var decompiled = LinqDecompiler.Decompile(foo);
            // int is implicitly converted to double before the add
            Assert.Equal(ExpressionType.Add, decompiled.Body.NodeType);
        }

        // ── extended method call target methods ───────────────────────────────

        static int CallMathAbs(int x) => Math.Abs(x);
        static double CallMathPow(double x, double y) => Math.Pow(x, y);
        static int ChainedMinMax(int x, int y, int z) => Math.Min(Math.Max(x, y), z);
        static bool StringContains(string s, string sub) => s.Contains(sub);
        static string StringTrimUpper(string s) => s.Trim().ToUpper();
        static int ParseAndMultiply(string s) => int.Parse(s) * 2;
        static string JoinTrimConcat(string a, string b) => string.Concat(a.Trim(), b.Trim());
        static string FormatInt(string fmt, int x) => string.Format(fmt, x);

        // ── extended method call tests ────────────────────────────────────────

        [Fact]
        public void Call_MathAbs_Decompiles()
        {
            Func<int, int> foo = x => Math.Abs(x);
            Expression<Func<int, int>> expected = x => Math.Abs(x);
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Call_MathPow_Decompiles()
        {
            Func<double, double, double> foo = (x, y) => Math.Pow(x, y);
            Expression<Func<double, double, double>> expected = (x, y) => Math.Pow(x, y);
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Call_ChainedMinMax_Decompiles()
        {
            Func<int, int, int, int> foo = (x, y, z) => Math.Min(Math.Max(x, y), z);
            Expression<Func<int, int, int, int>> expected = (x, y, z) => Math.Min(Math.Max(x, y), z);
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Call_StringContains_Decompiles()
        {
            Func<string, string, bool> foo = (s, sub) => s.Contains(sub);
            Expression<Func<string, string, bool>> expected = (s, sub) => s.Contains(sub);
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Call_StringTrimUpper_Decompiles()
        {
            Func<string, string> foo = s => s.Trim().ToUpper();
            Expression<Func<string, string>> expected = s => s.Trim().ToUpper();
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Call_ParseAndMultiply_Decompiles()
        {
            Func<string, int> foo = s => int.Parse(s) * 2;
            Expression<Func<string, int>> expected = s => int.Parse(s) * 2;
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        [Fact]
        public void Call_JoinTrimConcat_Decompiles()
        {
            Func<string, string, string> foo = (a, b) => string.Concat(a.Trim(), b.Trim());
            Expression<Func<string, string, string>> expected = (a, b) => string.Concat(a.Trim(), b.Trim());
            Assert.Equal(expected.ToString(), LinqDecompiler.Decompile(foo).ToString());
        }

        // ── array operation target methods ────────────────────────────────────

        static int GetElement(int[] arr, int i) => arr[i];
        static int ArrayLen(int[] arr) => arr.Length;
        static string GetStringElem(string[] arr, int i) => arr[i];

        // ── array operation tests ─────────────────────────────────────────────

        [Fact]
        public void Array_Sum_HasLdelemI4AndLdlen()
        {
            var il = new Func<int[], int>(arr => {
                int s = 0;
                for (int i = 0; i < arr.Length; i++) s += arr[i];
                return s;
            }).Method.GetInstructions();
            Assert.True(HasAnyOpcode(il, OpType.Ldelem_i4, OpType.Ldelem),
                "Array element access should use ldelem.i4 or ldelem");
            Assert.True(HasOpcode(il, OpType.Ldlen), "Array length access should use ldlen");
            Assert.Contains(il, z => z.IsLoopHeader);
        }

        [Fact]
        public void Array_NewArray_HasNewarrOpcode()
        {
            var il = new Func<int, int[]>(n => new int[n]).Method.GetInstructions();
            Assert.True(HasOpcode(il, OpType.Newarr));
        }

        [Fact]
        public void Decompile_ArrayElementAccess_ProducesArrayIndexNode()
        {
            Func<int[], int, int> foo = (arr, i) => arr[i];
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.ArrayIndex, decompiled.Body.NodeType);
        }

        [Fact]
        public void Decompile_ArrayLength_ContainsLengthAccess()
        {
            Func<int[], int> foo = arr => arr.Length;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Contains("Length", decompiled.ToString());
        }

        [Fact]
        public void Decompile_StringArrayElement_ProducesArrayIndexNode()
        {
            Func<string[], int, string> foo = (arr, i) => arr[i];
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(ExpressionType.ArrayIndex, decompiled.Body.NodeType);
        }

        // ── complex nested permutations ───────────────────────────────────────

        // try-catch → loop → nested-if
        static int TryCatchLoopNestedIf(string[] items)
        {
            int result = 0;
            try
            {
                for (int i = 0; i < items.Length; i++)
                {
                    int val = int.Parse(items[i]);
                    if (val > 0)
                    {
                        if (val > 100) result += 100;
                        else result += val;
                    }
                    else
                        result -= val;
                }
            }
            catch (FormatException)
            {
                return -1;
            }
            return result;
        }

        // nested try-catch → switch in finally
        static int NestedTrySwitchInFinally(string s, int mode)
        {
            int result = 0;
            try
            {
                result = int.Parse(s);
            }
            catch (FormatException)
            {
                result = -1;
            }
            catch (OverflowException)
            {
                result = -2;
            }
            finally
            {
                switch (mode)
                {
                    case 0: result = 0; break;
                    case 1: break;
                    default: result = -99; break;
                }
            }
            return result;
        }

        // loop → if → try-finally
        static int LoopIfTryFinally(int n)
        {
            int result = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 2 == 0)
                {
                    try { result += i; }
                    finally { result += 0; }
                }
            }
            return result;
        }

        // switch → nested-if → try-catch
        static string SwitchIfTryCatch(int x, string s)
        {
            switch (x)
            {
                case 0:
                    if (s != null)
                    {
                        try { return int.Parse(s).ToString(); }
                        catch (FormatException) { return "bad"; }
                    }
                    return "null";
                case 1:
                    if (s == null) return "null";
                    return s.ToUpper();
                default:
                    return "unknown";
            }
        }

        // Deeply nested: try(try(loop(switch(if))))
        static int DeepNesting(string[] items, int mode)
        {
            int result = 0;
            try
            {
                try
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        switch (mode)
                        {
                            case 0:
                                if (items[i] != null)
                                    result += int.Parse(items[i]);
                                break;
                            default:
                                result -= 1;
                                break;
                        }
                    }
                }
                catch (FormatException)
                {
                    result = -1;
                }
            }
            catch (Exception)
            {
                result = -99;
            }
            return result;
        }

        // ── complex nesting tests ─────────────────────────────────────────────

        [Fact]
        public void Complex_TryCatchLoopNestedIf_HasLoopEHAndManyLeaders()
        {
            var il = new Func<string[], int>(TryCatchLoopNestedIf).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.NotEmpty(il.ExceptionHandlers);
            Assert.True(il.Count(z => z.IsLeader) >= 4);
        }

        [Fact]
        public void Complex_NestedTrySwitchInFinally_HasThreeHandlers()
        {
            var il = new Func<string, int, int>(NestedTrySwitchInFinally).Method.GetInstructions();
            // 2 catches + 1 finally = 3 exception handlers
            Assert.Equal(3, il.ExceptionHandlers.Length);
            Assert.True(il.ExceptionHandlers.Any(h => h.IsFinally));
        }

        [Fact]
        public void Complex_LoopIfTryFinally_HasLoopAndFinallyHandler()
        {
            var il = new Func<int, int>(LoopIfTryFinally).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.Contains(il.ExceptionHandlers, h => h.IsFinally);
        }

        [Fact]
        public void Complex_SwitchIfTryCatch_HasEHAndManyLeaders()
        {
            var il = new Func<int, string, string>(SwitchIfTryCatch).Method.GetInstructions();
            Assert.NotEmpty(il.ExceptionHandlers);
            Assert.True(il.Count(z => z.IsLeader) >= 5);
        }

        [Fact]
        public void Complex_DeepNesting_HasLoopAndMultipleEHHandlers()
        {
            var il = new Func<string[], int, int>(DeepNesting).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.True(il.ExceptionHandlers.Length >= 2);
        }

        [Fact]
        public void Complex_DeepNesting_OuterEHEncompassesInner()
        {
            var il = new Func<string[], int, int>(DeepNesting).Method.GetInstructions();
            var catches = il.ExceptionHandlers.Where(h => h.IsCatch).ToArray();
            Assert.True(catches.Length >= 2);
            // Outer catch (Exception) try region encompasses inner catch (FormatException)
            bool hasFormatCatch = catches.Any(h => h.CatchType == typeof(FormatException));
            bool hasExceptionCatch = catches.Any(h => h.CatchType == typeof(Exception));
            if (hasFormatCatch && hasExceptionCatch)
            {
                var inner = catches.First(h => h.CatchType == typeof(FormatException));
                var outer = catches.First(h => h.CatchType == typeof(Exception));
                Assert.True(outer.TryStart <= inner.TryStart);
                Assert.True(outer.TryEnd >= inner.TryEnd);
            }
        }

        // ── exception handler boundary and ordering tests ─────────────────────

        static int FourCatchesOrdering(string s)
        {
            try
            {
                return int.Parse(s);
            }
            catch (FormatException) { return -1; }
            catch (OverflowException) { return -2; }
            catch (ArgumentNullException) { return -3; }
            catch (Exception) { return -99; }
        }

        static int RethrowInFinally(string s)
        {
            int result = 0;
            try
            {
                result = int.Parse(s);
            }
            catch (FormatException)
            {
                throw;
            }
            finally
            {
                result += 0;
            }
            return result;
        }

        static int NestedTryWithInnerFinally(string s)
        {
            try
            {
                try { return int.Parse(s); }
                finally { /* inner cleanup */ }
            }
            catch (Exception) { return -1; }
        }

        // ── exception handler ordering tests ─────────────────────────────────

        [Fact]
        public void EH_FourCatches_AllCatchesShareSameTryStart()
        {
            var il = new Func<string, int>(FourCatchesOrdering).Method.GetInstructions();
            Assert.Equal(4, il.ExceptionHandlers.Length);
            Assert.All(il.ExceptionHandlers, h => Assert.True(h.IsCatch));
            var tryStarts = il.ExceptionHandlers.Select(h => h.TryStart).Distinct().ToArray();
            Assert.Single(tryStarts);
        }

        [Fact]
        public void EH_FourCatches_ContainsAllExpectedTypes()
        {
            var il = new Func<string, int>(FourCatchesOrdering).Method.GetInstructions();
            var types = il.ExceptionHandlers.Select(h => h.CatchType).ToArray();
            Assert.Contains(typeof(FormatException), types);
            Assert.Contains(typeof(OverflowException), types);
            Assert.Contains(typeof(ArgumentNullException), types);
            Assert.Contains(typeof(Exception), types);
        }

        [Fact]
        public void EH_FourCatches_AllHandlerStartsAreLeaders()
        {
            var il = new Func<string, int>(FourCatchesOrdering).Method.GetInstructions();
            foreach (var h in il.ExceptionHandlers)
                Assert.True(il[h.HandlerStart].IsLeader,
                    $"Handler at {h.HandlerStart} should be a leader");
        }

        [Fact]
        public void EH_FourCatches_AllTryStartsAreLeaders()
        {
            var il = new Func<string, int>(FourCatchesOrdering).Method.GetInstructions();
            foreach (var h in il.ExceptionHandlers)
                Assert.True(il[h.TryStart].IsLeader,
                    $"Try start at {h.TryStart} should be a leader");
        }

        [Fact]
        public void EH_RethrowInFinally_HasCatchFinallyAndRethrow()
        {
            var il = new Func<string, int>(RethrowInFinally).Method.GetInstructions();
            Assert.True(il.ExceptionHandlers.Any(h => h.IsCatch));
            Assert.True(il.ExceptionHandlers.Any(h => h.IsFinally));
            Assert.True(HasOpcode(il, OpType.Rethrow));
        }

        [Fact]
        public void EH_NestedTryWithInnerFinally_HasFinallyAndCatch()
        {
            var il = new Func<string, int>(NestedTryWithInnerFinally).Method.GetInstructions();
            Assert.True(il.ExceptionHandlers.Any(h => h.IsFinally));
            Assert.True(il.ExceptionHandlers.Any(h => h.IsCatch));
        }

        [Fact]
        public void EH_NestedTryWithInnerFinally_InnerFinallyInsideOuterTry()
        {
            var il = new Func<string, int>(NestedTryWithInnerFinally).Method.GetInstructions();
            var finallyH = il.ExceptionHandlers.First(h => h.IsFinally);
            var catchH = il.ExceptionHandlers.First(h => h.IsCatch);
            // The finally's try region is within the catch's try region
            Assert.True(catchH.TryStart <= finallyH.TryStart);
        }

        // ── ILReader and InstructionList sanity tests ─────────────────────────

        [Fact]
        public void ILReader_LoopMethod_ExposesLoopHeaderViaInstructionList()
        {
            var il = new Func<int, int>(ForLoopSum).Method.GetInstructions();
            var reader = new Func<int, int>(ForLoopSum).Method.GetILReader();
            // Both surfaces should have exception handlers count = 0
            Assert.Empty(reader.ExceptionHandlers);
            Assert.Empty(il.ExceptionHandlers);
        }

        [Fact]
        public void ILReader_TryCatchMethod_ExposesHandlersViaReader()
        {
            var reader = new Func<string, int>(FilteredCatch1).Method.GetILReader();
            Assert.Single(reader.ExceptionHandlers);
            Assert.True(reader.ExceptionHandlers[0].IsFilter);
        }

        [Fact]
        public void InstructionList_AllLoopHeaders_AreLeaders()
        {
            var il = new Func<int, int>(TripleNestedLoop).Method.GetInstructions();
            for (int i = 0; i < il.Count; i++)
                if (il[i].IsLoopHeader)
                    Assert.True(il[i].IsLeader,
                        $"Loop header at index {i} must also be a leader");
        }

        [Fact]
        public void InstructionList_GetIndex_ReturnsValidIndexForHandlerLabels()
        {
            var il = new Func<string, int>(FilteredCatch1).Method.GetInstructions();
            var h = il.ExceptionHandlers[0];
            var tryIdx = il.GetIndex(h.TryStart);
            var handlerIdx = il.GetIndex(h.HandlerStart);
            Assert.True(tryIdx >= 0 && tryIdx < il.Count,
                "TryStart index should be within bounds");
            Assert.True(handlerIdx >= 0 && handlerIdx < il.Count,
                "HandlerStart index should be within bounds");
            Assert.True(tryIdx < handlerIdx,
                "Try region must precede handler region in the instruction stream");
        }
    }
}
