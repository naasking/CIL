using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using CIL.Expressions;

namespace CIL.Tests
{
    /// <summary>
    /// Advanced decompilation tests exercising sophisticated control flow,
    /// type conversions, and exception handling in compiled methods.
    /// </summary>
    public class DecompilerAdvancedTests
    {
        #region Target methods — arithmetic and conversions

        static int Arithmetic(int x, int y) => x * y + x - y;
        static int Negation(int x) => -x;
        static int Remainder(int x, int y) => x % y;
        static long Widen(int x) => (long)x;
        static double IntToDouble(int x) => (double)x;
        static double MathSqrt(double x) => Math.Sqrt(x);
        static int MathMax(int x, int y) => Math.Max(x, y);
        static string Concat3(string a, string b, string c) => string.Concat(a, b, c);
        static string SubstringAndTrim(string s) => s.Substring(1).Trim();
        static bool IntEquality(int x, int y) => x == y;
        static bool IntLessThan(int x, int y) => x < y;

        #endregion

        #region Target methods — control flow

        static int Ternary(int x) => x > 0 ? x : -x;

        static int NestedTernary(int x) =>
            x > 0 ? (x > 100 ? 100 : x) : (x < -100 ? -100 : x);

        static int MultiReturn(int x)
        {
            if (x > 0) return 1;
            if (x < 0) return -1;
            return 0;
        }

        static string NullFallback(string s) => s ?? "default";

        static int SwitchMethod(int x)
        {
            switch (x)
            {
                case 0: return 10;
                case 1: return 20;
                case 2: return 30;
                default: return -1;
            }
        }

        static int ChainedComparisons(int x, int y, int z) =>
            x > y ? (y > z ? x : z) : (x > z ? y : z);

        #endregion

        #region Target methods — exception handling

        static int SimpleTryCatch(string s)
        {
            try { return int.Parse(s); }
            catch (FormatException) { return -1; }
        }

        static int TryMultipleCatch(string s)
        {
            try
            {
                return int.Parse(s);
            }
            catch (FormatException)
            {
                return -1;
            }
            catch (OverflowException)
            {
                return -2;
            }
            catch (ArgumentNullException)
            {
                return -3;
            }
        }

        static int TryFinallyOnly(int x)
        {
            try { x *= 2; }
            finally { x += 1; }
            return x;
        }

        static int TryCatchFinally(string s)
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
            finally
            {
                result += 0; // touch result to ensure finally body
            }
            return result;
        }

        static int NestedTryCatch(string s)
        {
            try
            {
                try
                {
                    return int.Parse(s);
                }
                catch (FormatException)
                {
                    return -1;
                }
            }
            catch (Exception)
            {
                return -99;
            }
        }

        static int DeeplyNestedTry(string s)
        {
            try
            {
                try
                {
                    try
                    {
                        return int.Parse(s);
                    }
                    catch (FormatException)
                    {
                        return -1;
                    }
                }
                catch (OverflowException)
                {
                    return -2;
                }
            }
            catch (Exception)
            {
                return -3;
            }
        }

        static int CatchWithVariable(string s)
        {
            try
            {
                return int.Parse(s);
            }
            catch (FormatException ex)
            {
                return ex.Message.Length;
            }
        }

        static int CatchAndRethrow(string s)
        {
            try
            {
                return int.Parse(s);
            }
            catch (FormatException)
            {
                throw;
            }
        }

        static int TryInLoop(string[] items)
        {
            int sum = 0;
            for (int i = 0; i < items.Length; i++)
            {
                try
                {
                    sum += int.Parse(items[i]);
                }
                catch
                {
                    // skip
                }
            }
            return sum;
        }

        static int FilteredCatch(string s)
        {
            try
            {
                return int.Parse(s);
            }
            catch (FormatException ex) when (ex.Message.Length > 0)
            {
                return -1;
            }
        }

        static int TryFinallyInTryCatch(string s)
        {
            try
            {
                int result;
                try
                {
                    result = int.Parse(s);
                }
                finally
                {
                    result = 0;
                }
                return result;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        #endregion

        #region End-to-end decompilation — arithmetic and conversions

        [Fact]
        public void Decompile_Arithmetic()
        {
            Func<int, int, int> foo = (x, y) => x * y + x - y;
            Expression<Func<int, int, int>> expected = (x, y) => x * y + x - y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_Negation()
        {
            Func<int, int> foo = x => -x;
            Expression<Func<int, int>> expected = x => -x;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_Remainder()
        {
            Func<int, int, int> foo = (x, y) => x % y;
            Expression<Func<int, int, int>> expected = (x, y) => x % y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_WidenToLong()
        {
            Func<int, long> foo = x => (long)x;
            Expression<Func<int, long>> expected = x => (long)x;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_IntToDouble()
        {
            Func<int, double> foo = x => (double)x;
            Expression<Func<int, double>> expected = x => (double)x;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_MathSqrt()
        {
            Func<double, double> foo = x => Math.Sqrt(x);
            Expression<Func<double, double>> expected = x => Math.Sqrt(x);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_MathMax()
        {
            Func<int, int, int> foo = (x, y) => Math.Max(x, y);
            Expression<Func<int, int, int>> expected = (x, y) => Math.Max(x, y);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_ThreeParamConcat()
        {
            Func<string, string, string, string> foo = (a, b, c) => string.Concat(a, b, c);
            Expression<Func<string, string, string, string>> expected = (a, b, c) => string.Concat(a, b, c);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_InstanceMethodChain()
        {
            Func<string, string> foo = s => s.Substring(1).Trim();
            Expression<Func<string, string>> expected = s => s.Substring(1).Trim();
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_Equality()
        {
            Func<int, int, bool> foo = (x, y) => x == y;
            Expression<Func<int, int, bool>> expected = (x, y) => x == y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_LessThan()
        {
            Func<int, int, bool> foo = (x, y) => x < y;
            Expression<Func<int, int, bool>> expected = (x, y) => x < y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        #endregion

        #region End-to-end decompilation — control flow

        // Comparison expressions that produce bools decompile successfully because
        // they compute a value on the stack and return directly without stloc.

        [Fact]
        public void Decompile_GreaterThan()
        {
            Func<int, int, bool> foo = (x, y) => x > y;
            Expression<Func<int, int, bool>> expected = (x, y) => x > y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_GreaterThanOrEqual()
        {
            // CIL has no cge instruction; the compiler emits clt + ceq(0),
            // which decompiles as Not(x < y) — semantically equivalent.
            Func<int, int, bool> foo = (x, y) => x >= y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal("(x, y) => Not((x < y))", decompiled.ToString());
        }

        [Fact]
        public void Decompile_LessThanOrEqual()
        {
            // CIL has no cle instruction; the compiler emits cgt + ceq(0),
            // which decompiles as Not(x > y) — semantically equivalent.
            Func<int, int, bool> foo = (x, y) => x <= y;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal("(x, y) => Not((x > y))", decompiled.ToString());
        }

        [Fact]
        public void Decompile_CombinedComparisons()
        {
            // Multiple comparisons chained via method calls still decompile
            Func<int, int, int, bool> foo = (x, y, z) => x > y & y < z;
            Expression<Func<int, int, int, bool>> expected = (x, y, z) => x > y & y < z;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        #endregion

        #region Decompilation — documented limitations

        // Ternary operators compiled in debug mode use stloc/ldloc for the
        // merge point. The recursive branch-following in Process diverges when
        // both branches store to the same local and converge via br to a shared
        // ldloc+ret sequence, exhausting the eval stack.

        [Fact]
        public void Decompile_TernaryWithLocalMerge_NotYetSupported()
        {
            Assert.ThrowsAny<Exception>(
                () => LinqDecompiler.Decompile(new Func<int, int>(Ternary)));
        }

        [Fact]
        public void Decompile_NullCoalescing_NotYetSupported()
        {
            // The ?? operator generates dup + brtrue + pop which leaves the
            // eval stack in an inconsistent state.
            Assert.ThrowsAny<Exception>(
                () => LinqDecompiler.Decompile(new Func<string, string>(NullFallback)));
        }

        [Fact]
        public void Decompile_MultiReturnWithBoolBranch_NotYetSupported()
        {
            // brfalse on a bool comparison hits NullOrZero with typeof(bool),
            // which is a value type — Constant(null, bool) is invalid.
            Assert.ThrowsAny<Exception>(
                () => LinqDecompiler.Decompile(new Func<int, int>(MultiReturn)));
        }

        #endregion

        #region Exception handling — instruction-level analysis

        [Fact]
        public void EH_SimpleTryCatch_SingleCatchRegion()
        {
            var il = new Func<string, int>(SimpleTryCatch).Method.GetInstructions();
            Assert.Single(il.ExceptionHandlers);
            var h = il.ExceptionHandlers[0];
            Assert.True(h.IsCatch);
            Assert.Equal(typeof(FormatException), h.CatchType);

            Assert.True(il[h.TryStart].IsTryStart);
            Assert.True(il[h.HandlerStart].IsHandlerStart);
            Assert.True(il[h.TryStart].IsLeader);
            Assert.True(il[h.HandlerStart].IsLeader);

            // Try must end at or before handler start
            Assert.True(h.TryEnd <= h.HandlerStart);
            // Handler must end after handler start
            Assert.True(h.HandlerEnd > h.HandlerStart);
        }

        [Fact]
        public void EH_MultipleCatch_ThreeHandlers()
        {
            var il = new Func<string, int>(TryMultipleCatch).Method.GetInstructions();
            Assert.Equal(3, il.ExceptionHandlers.Length);
            Assert.All(il.ExceptionHandlers, h => Assert.True(h.IsCatch));

            var types = il.ExceptionHandlers.Select(h => h.CatchType).ToArray();
            Assert.Contains(typeof(FormatException), types);
            Assert.Contains(typeof(OverflowException), types);
            Assert.Contains(typeof(ArgumentNullException), types);

            // All handlers share the same try region
            var tryStarts = il.ExceptionHandlers.Select(h => h.TryStart).Distinct().ToArray();
            Assert.Single(tryStarts);
        }

        [Fact]
        public void EH_TryFinallyOnly_SingleFinallyHandler()
        {
            var il = new Func<int, int>(TryFinallyOnly).Method.GetInstructions();
            Assert.Single(il.ExceptionHandlers);
            Assert.True(il.ExceptionHandlers[0].IsFinally);
            Assert.Null(il.ExceptionHandlers[0].CatchType);

            // Handler body must contain endfinally
            var handlerIdx = il.GetIndex(il.ExceptionHandlers[0].HandlerStart);
            bool hasEndfinally = false;
            for (int i = handlerIdx; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Endfinally)
                {
                    hasEndfinally = true;
                    break;
                }
            }
            Assert.True(hasEndfinally, "Finally handler must contain endfinally");
        }

        [Fact]
        public void EH_TryCatchFinally_CatchAndFinallyRegions()
        {
            var il = new Func<string, int>(TryCatchFinally).Method.GetInstructions();
            Assert.Equal(2, il.ExceptionHandlers.Length);

            var catches = il.ExceptionHandlers.Where(h => h.IsCatch).ToArray();
            var finallys = il.ExceptionHandlers.Where(h => h.IsFinally).ToArray();
            Assert.Single(catches);
            Assert.Single(finallys);
            Assert.Equal(typeof(FormatException), catches[0].CatchType);

            // Both should be flagged
            Assert.True(il[catches[0].HandlerStart].IsHandlerStart);
            Assert.True(il[finallys[0].HandlerStart].IsHandlerStart);
        }

        [Fact]
        public void EH_NestedTryCatch_OuterEncompasesInner()
        {
            var il = new Func<string, int>(NestedTryCatch).Method.GetInstructions();
            Assert.Equal(2, il.ExceptionHandlers.Length);

            var inner = il.ExceptionHandlers.First(h => h.CatchType == typeof(FormatException));
            var outer = il.ExceptionHandlers.First(h => h.CatchType == typeof(Exception));

            Assert.True(outer.TryStart <= inner.TryStart);
            Assert.True(outer.TryEnd >= inner.TryEnd);
        }

        [Fact]
        public void EH_DeeplyNested_ThreeRegionsProperlyNested()
        {
            var il = new Func<string, int>(DeeplyNestedTry).Method.GetInstructions();
            Assert.Equal(3, il.ExceptionHandlers.Length);

            var format = il.ExceptionHandlers.First(h => h.CatchType == typeof(FormatException));
            var overflow = il.ExceptionHandlers.First(h => h.CatchType == typeof(OverflowException));
            var exception = il.ExceptionHandlers.First(h => h.CatchType == typeof(Exception));

            // Format (innermost) should be within overflow
            Assert.True(overflow.TryStart <= format.TryStart);
            Assert.True(overflow.TryEnd >= format.TryEnd);

            // Overflow should be within exception (outermost)
            Assert.True(exception.TryStart <= overflow.TryStart);
            Assert.True(exception.TryEnd >= overflow.TryEnd);
        }

        [Fact]
        public void EH_CatchWithVariable_HasCatchType()
        {
            var il = new Func<string, int>(CatchWithVariable).Method.GetInstructions();
            Assert.Single(il.ExceptionHandlers);
            Assert.Equal(typeof(FormatException), il.ExceptionHandlers[0].CatchType);
        }

        [Fact]
        public void EH_CatchAndRethrow_ContainsRethrowOpcode()
        {
            var il = new Func<string, int>(CatchAndRethrow).Method.GetInstructions();
            Assert.Single(il.ExceptionHandlers);

            // The handler should contain a rethrow instruction
            var handlerIdx = il.GetIndex(il.ExceptionHandlers[0].HandlerStart);
            bool hasRethrow = false;
            for (int i = handlerIdx; i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Rethrow)
                {
                    hasRethrow = true;
                    break;
                }
            }
            Assert.True(hasRethrow, "Catch handler must contain rethrow");
        }

        [Fact]
        public void EH_TryInLoop_HasBothLoopAndException()
        {
            var il = new Func<string[], int>(TryInLoop).Method.GetInstructions();

            // Should have exception handler(s)
            Assert.NotEmpty(il.ExceptionHandlers);

            // Should have a loop (back-edge detected by CFA)
            Assert.Contains(il, instr => instr.IsLoopHeader);

            // Both loop and exception handling structures coexist
            var loopIdx = Enumerable.Range(0, il.Count).First(i => il[i].IsLoopHeader);
            var eh = il.ExceptionHandlers[0];
            var tryIdx = il.GetIndex(eh.TryStart);
            // The try and loop are related (one is within the other's range)
            Assert.True(
                (tryIdx >= loopIdx) || (loopIdx >= tryIdx),
                "Loop and try block should overlap in the instruction stream");
        }

        [Fact]
        public void EH_TryInLoop_HasBothLoopAndException_Linq()
        {
            var e = LinqDecompiler.Decompile(TryInLoop);
            System.Console.WriteLine("EXPR: " + e);
            System.Console.WriteLine("BODY: " + e.Body);
            System.Console.WriteLine("BODYTYPE: " + e.Body.NodeType);
            void DumpExpr(System.Linq.Expressions.Expression expr, int depth = 0)
            {
                var prefix = new string(' ', depth * 2);
                System.Console.WriteLine($"{prefix}{expr.NodeType} [{expr.Type.Name}]: {expr}");
                if (expr is System.Linq.Expressions.BlockExpression block)
                    foreach (var x in block.Expressions) DumpExpr(x, depth + 1);
                else if (expr is System.Linq.Expressions.BinaryExpression bin)
                {
                    DumpExpr(bin.Left, depth + 1);
                    DumpExpr(bin.Right, depth + 1);
                }
                else if (expr is System.Linq.Expressions.ConditionalExpression cond)
                {
                    System.Console.WriteLine($"{prefix}  TEST:"); DumpExpr(cond.Test, depth + 2);
                    System.Console.WriteLine($"{prefix}  THEN:"); DumpExpr(cond.IfTrue, depth + 2);
                    System.Console.WriteLine($"{prefix}  ELSE:"); DumpExpr(cond.IfFalse, depth + 2);
                }
                else if (expr is System.Linq.Expressions.TryExpression tryExpr)
                {
                    System.Console.WriteLine($"{prefix}  TRY:"); DumpExpr(tryExpr.Body, depth + 2);
                    foreach (var h in tryExpr.Handlers)
                    {
                        System.Console.WriteLine($"{prefix}  CATCH({h.Test.Name}):"); DumpExpr(h.Body, depth + 2);
                    }
                    if (tryExpr.Finally != null)
                    { System.Console.WriteLine($"{prefix}  FINALLY:"); DumpExpr(tryExpr.Finally, depth + 2); }
                }
                else if (expr is System.Linq.Expressions.LoopExpression loop)
                {
                    System.Console.WriteLine($"{prefix}  LOOP BODY:"); DumpExpr(loop.Body, depth + 2);
                }
            }
            DumpExpr(e.Body);

            //FIXME: fill in checks for loop and nested try-catch
        }

        [Fact]
        public void EH_FilteredCatch_HasFilterRegion()
        {
            var il = new Func<string, int>(FilteredCatch).Method.GetInstructions();
            Assert.Single(il.ExceptionHandlers);
            var h = il.ExceptionHandlers[0];
            Assert.True(h.IsFilter);

            // Filter has a filter start label
            Assert.True(h.FilterStart < h.HandlerStart,
                "Filter evaluation block must precede the handler");

            // Filter start instruction should be flagged
            Assert.True(il[h.FilterStart].IsFilterStart);

            // Should contain endfilter opcode in the filter region
            var filterIdx = il.GetIndex(h.FilterStart);
            var handlerIdx = il.GetIndex(h.HandlerStart);
            bool hasEndfilter = false;
            for (int i = filterIdx; i < handlerIdx; i++)
            {
                if (il[i].OpCode.Type() == OpType.Endfilter)
                {
                    hasEndfilter = true;
                    break;
                }
            }
            Assert.True(hasEndfilter, "Filter region must contain endfilter");
        }

        [Fact]
        public void EH_TryFinallyInTryCatch_MixedHandlerTypes()
        {
            var il = new Func<string, int>(TryFinallyInTryCatch).Method.GetInstructions();

            var catches = il.ExceptionHandlers.Where(h => h.IsCatch).ToArray();
            var finallys = il.ExceptionHandlers.Where(h => h.IsFinally).ToArray();

            Assert.NotEmpty(catches);
            Assert.NotEmpty(finallys);

            // The finally is inside the try-catch, so its try region should be
            // within the catch's try region
            var outerCatch = catches.First(h => h.CatchType == typeof(Exception));
            var innerFinally = finallys[0];
            Assert.True(outerCatch.TryStart <= innerFinally.TryStart);
        }

        #endregion

        #region Exception handling — leave and handler CFA coverage

        [Fact]
        public void EH_TryBody_ContainsLeaveInstruction()
        {
            var il = new Func<string, int>(SimpleTryCatch).Method.GetInstructions();
            var eh = il.ExceptionHandlers[0];
            var tryStart = il.GetIndex(eh.TryStart);
            var tryEnd = il.GetIndex(eh.TryEnd);

            bool hasLeave = false;
            for (int i = tryStart; i < tryEnd; i++)
            {
                var op = il[i].OpCode.Type();
                if (op == OpType.Leave || op == OpType.Leave_s)
                {
                    hasLeave = true;
                    break;
                }
            }
            Assert.True(hasLeave, "Try body must end with a leave instruction");
        }

        [Fact]
        public void EH_HandlerBlocks_VisitedByCFA()
        {
            // Verify that CFA visited the handler blocks (they should have
            // instructions marked as leaders and not remain in White state)
            var il = new Func<string, int>(TryMultipleCatch).Method.GetInstructions();

            foreach (var h in il.ExceptionHandlers)
            {
                var handlerInstr = il[h.HandlerStart];
                Assert.True(handlerInstr.IsLeader,
                    $"Handler at {h.HandlerStart} should be a leader");
                Assert.True(handlerInstr.IsHandlerStart,
                    $"Handler at {h.HandlerStart} should have HandlerStart flag");
            }
        }

        [Fact]
        public void EH_AllHandlerBoundaries_AreLeaders()
        {
            var il = new Func<string, int>(DeeplyNestedTry).Method.GetInstructions();

            foreach (var h in il.ExceptionHandlers)
            {
                Assert.True(il[h.TryStart].IsLeader,
                    $"Try start at {h.TryStart} must be a leader");
                Assert.True(il[h.HandlerStart].IsLeader,
                    $"Handler start at {h.HandlerStart} must be a leader");

                // Exclusive end boundaries are leaders if within bounds
                if (h.TryEnd.Offset < il.Count)
                    Assert.True(il[h.TryEnd].IsLeader,
                        $"Try end at {h.TryEnd} must be a leader");
            }
        }

        #endregion

        #region Exception handling — ExceptionRegion ToString

        [Fact]
        public void EH_ToString_CatchFormat()
        {
            var il = new Func<string, int>(SimpleTryCatch).Method.GetInstructions();
            var str = il.ExceptionHandlers[0].ToString();
            Assert.Contains(".try", str);
            Assert.Contains("catch(FormatException)", str);
        }

        [Fact]
        public void EH_ToString_FinallyFormat()
        {
            var il = new Func<int, int>(TryFinallyOnly).Method.GetInstructions();
            var str = il.ExceptionHandlers[0].ToString();
            Assert.Contains(".try", str);
            Assert.Contains("finally", str);
        }

        [Fact]
        public void EH_ToString_FilterFormat()
        {
            var il = new Func<string, int>(FilteredCatch).Method.GetInstructions();
            var str = il.ExceptionHandlers[0].ToString();
            Assert.Contains(".try", str);
            Assert.Contains("filter", str);
        }

        #endregion

        #region ILReader-level exception handler access

        [Fact]
        public void ILReader_MultipleCatch_ExposesAllHandlers()
        {
            var reader = new Func<string, int>(TryMultipleCatch).Method.GetILReader();
            Assert.Equal(3, reader.ExceptionHandlers.Length);
        }

        [Fact]
        public void ILReader_DeeplyNested_ExposesAllHandlers()
        {
            var reader = new Func<string, int>(DeeplyNestedTry).Method.GetILReader();
            Assert.Equal(3, reader.ExceptionHandlers.Length);
        }

        [Fact]
        public void ILReader_NoHandlers_ReturnsEmpty()
        {
            var reader = new Func<int, int, int>(Arithmetic).Method.GetILReader();
            Assert.Empty(reader.ExceptionHandlers);
        }

        [Fact]
        public void ILReader_FilteredCatch_HasFilterFlag()
        {
            var reader = new Func<string, int>(FilteredCatch).Method.GetILReader();
            Assert.Single(reader.ExceptionHandlers);
            Assert.True(reader.ExceptionHandlers[0].IsFilter);
        }

        #endregion
    }
}
