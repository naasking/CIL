using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using Xunit;
using CIL.Expressions;

namespace CIL.Tests
{
    public class SimpleTests
    {
        [Fact]
        public static void TestSimple()
        {
            Func<int> foo = () => 3;
            Expression<Func<int>> fooe = () => 3;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }
        [Fact]
        public static void TestEqConst()
        {
            Func<bool> foo = () => 3 != 5;
            Expression<Func<bool>> fooe = () => true;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }

        [Fact]
        public static void TestEqParam()
        {
            Func<int, bool> foo = x => 3 != x;
            Expression<Func<int, bool>> fooe = x => 3 != x;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }

        [Fact]
        public static void TestEqParamReverse()
        {
            Func<int, bool> foo = x => x != 3;
            Expression<Func<int, bool>> fooe = x => x != 3;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }
        [Fact]
        public static void TestSwitch()
        {
            Func<int, bool> foo = x =>
            {
                switch (x)
                {
                    case 0: return true;
                    default: return false;
                };
            };
            var decompiled = LinqDecompiler.Decompile(foo);
        }

        [Fact]
        public static void TestStringOps()
        {
            Func<string, string> foo = x => x.Substring(3).Replace(';', ':').PadRight(3);
            Expression<Func<string, string>> fooe = x => x.Substring(3).Replace(';', ':').PadRight(3);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }

        //[Fact]
        //public static void TestBoolOps()
        //{
        //    Func<bool, bool, bool> foo = (x,y) => x || !x && y;
        //    Expression<Func<bool, bool, bool>> fooe = (x, y) => x || !x && y;
        //    var decompiled = foo.GetExpression();
        //    Assert.Equal(fooe.ToString(), decompiled.ToString());
        //    Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        //}

        [Fact]
        public static void TestCalls()
        {
            var main = new Action<string[]>(MainOrig);
            var methods = typeof(SimpleTests).GetMethods().Where(x => x != main.Method).ToArray();
            var il = main.Method.GetILReader();
            while (il.MoveNext())
            {
                switch (il.Current.OpCode.Type())
                {
                    case OpType.Call:
                        Assert.Contains(il.Current.ResolveMethod(), methods);
                        break;
                }
            }
        }

        [Fact]
        public static void TestOtherCalls()
        {
            var target = typeof(Module).GetMethod("ResolveMethod", new[] { typeof(int), typeof(Type[]), typeof(Type[]) }, null);
            var resolve = typeof(ILReader).GetMethod("ResolveMethod", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
            var il = resolve.GetILReader();
            while (il.MoveNext())
            {
                switch (il.Current.OpCode.Type())
                {
                    case OpType.Callvirt:
                        Assert.Equal(target, il.Current.ResolveMethod());
                        break;
                }
            }
        }

        class Foo
        {
            public string Bar { get; set; }
            public string this[int i]
            {
                get { return null; }
                set { }
            }
        }

        [Fact]
        public static void TestProperties()
        {
            Func<Foo, string> getFoo = x => x.Bar;
            Expression<Func<Foo, string>> getFooE = x => x.Bar;
            var e = LinqDecompiler.Decompile(getFoo);
            Assert.Equal(getFooE.ToString(), e.ToString());
        }

        [Fact]
        public static void TestPropertyIndexers()
        {
            Func<Foo, string> getFoo = x => x[2];
            Expression<Func<Foo, string>> getFooE = x => x[2];
            var e = LinqDecompiler.Decompile(getFoo);
            Assert.Equal(getFooE.ToString(), e.ToString());
        }

        #region Exception handling test methods

        static int TryCatchMethod(string s)
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

        static int TryFinallyMethod(int x)
        {
            try
            {
                x = x * 2;
            }
            finally
            {
                x = x + 1;
            }
            return x;
        }

        static int TryCatchFinallyMethod(string s)
        {
            int result;
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
                result = 0;
            }
            return result;
        }

        static int NestedTryCatchMethod(string s)
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
                return -2;
            }
        }

        #endregion

        [Fact]
        public static void TestTryCatch()
        {
            var il = new Func<string, int>(TryCatchMethod).Method.GetInstructions();
            var handlers = il.ExceptionHandlers;

            Assert.Single(handlers);
            Assert.True(handlers[0].IsCatch);
            Assert.Equal(typeof(FormatException), handlers[0].CatchType);

            // try start instruction should be flagged
            Assert.True(il[handlers[0].TryStart].IsTryStart);
            Assert.True(il[handlers[0].TryStart].IsLeader);

            // handler start instruction should be flagged
            Assert.True(il[handlers[0].HandlerStart].IsHandlerStart);
            Assert.True(il[handlers[0].HandlerStart].IsLeader);

            // try region must precede handler region
            Assert.True(handlers[0].TryStart < handlers[0].HandlerStart);
            Assert.True(handlers[0].TryEnd <= handlers[0].HandlerStart);
        }

        [Fact]
        public static void TestTryFinally()
        {
            var il = new Func<int, int>(TryFinallyMethod).Method.GetInstructions();
            var handlers = il.ExceptionHandlers;

            Assert.Single(handlers);
            Assert.True(handlers[0].IsFinally);
            Assert.Null(handlers[0].CatchType);

            Assert.True(il[handlers[0].TryStart].IsTryStart);
            Assert.True(il[handlers[0].HandlerStart].IsHandlerStart);

            // the handler must contain an endfinally
            bool foundEndFinally = false;
            for (int i = il.GetIndex(handlers[0].HandlerStart); i < il.Count; i++)
            {
                if (il[i].OpCode.Type() == OpType.Endfinally)
                {
                    foundEndFinally = true;
                    break;
                }
            }
            Assert.True(foundEndFinally);
        }

        [Fact]
        public static void TestTryCatchFinally()
        {
            var il = new Func<string, int>(TryCatchFinallyMethod).Method.GetInstructions();
            var handlers = il.ExceptionHandlers;

            // should have 3 regions: catch(FormatException), catch(OverflowException), finally
            Assert.Equal(3, handlers.Length);

            var catches = handlers.Where(h => h.IsCatch).ToArray();
            var finallys = handlers.Where(h => h.IsFinally).ToArray();
            Assert.Equal(2, catches.Length);
            Assert.Single(finallys);

            // verify catch types
            var catchTypes = catches.Select(c => c.CatchType).ToArray();
            Assert.Contains(typeof(FormatException), catchTypes);
            Assert.Contains(typeof(OverflowException), catchTypes);

            // all handlers should be flagged
            foreach (var h in handlers)
            {
                Assert.True(il[h.TryStart].IsTryStart);
                Assert.True(il[h.HandlerStart].IsHandlerStart);
            }
        }

        [Fact]
        public static void TestNestedTryCatch()
        {
            var il = new Func<string, int>(NestedTryCatchMethod).Method.GetInstructions();
            var handlers = il.ExceptionHandlers;

            Assert.Equal(2, handlers.Length);

            // both should be catches
            Assert.All(handlers, h => Assert.True(h.IsCatch));

            var catchTypes = handlers.Select(c => c.CatchType).ToArray();
            Assert.Contains(typeof(FormatException), catchTypes);
            Assert.Contains(typeof(Exception), catchTypes);

            // the outer try should encompass the inner try
            var inner = handlers.First(h => h.CatchType == typeof(FormatException));
            var outer = handlers.First(h => h.CatchType == typeof(Exception));
            Assert.True(outer.TryStart <= inner.TryStart);
            Assert.True(outer.TryEnd >= inner.TryEnd);
        }

        [Fact]
        public static void TestExceptionRegionToString()
        {
            var il = new Func<string, int>(TryCatchMethod).Method.GetInstructions();
            var h = il.ExceptionHandlers[0];
            var str = h.ToString();
            Assert.Contains(".try", str);
            Assert.Contains("catch(FormatException)", str);
        }

        [Fact]
        public static void TestNoExceptionHandlers()
        {
            var il = new Func<int, int>(LoopMethod).Method.GetInstructions();
            Assert.Empty(il.ExceptionHandlers);
            // no instructions should have exception flags
            Assert.DoesNotContain(il, i => i.IsTryStart);
            Assert.DoesNotContain(il, i => i.IsHandlerStart);
        }

        [Fact]
        public static void TestILReaderExceptionHandlers()
        {
            var reader = new Func<string, int>(TryCatchFinallyMethod).Method.GetILReader();
            Assert.Equal(3, reader.ExceptionHandlers.Length);
        }

        static void MainOrig(string[] args)
        {
            TestSimple();
            TestEqConst();
            TestEqParam();
            TestEqParamReverse();
            TestStringOps();
            TestCalls();
            TestOtherCalls();
            TestProperties();
            TestPropertyIndexers();
            //TestBoolOps();
            //TestSwitch();
        }


        static int LoopMethod(int i)
        {
            while (i > 0)
            {
                if (i % 2 == 0)
                {
                    i -= 3;
                    continue;
                }
                if (i > 100)
                    break;
                --i;
            }
            return i;
        }

        [Fact]
        public static void TestLoop()
        {
            var il = new Func<int, int>(LoopMethod).Method.GetInstructions();
            Assert.Contains(il, z => z.IsLoopHeader);
            Assert.Contains(il, z => z.IsLeader);
            var idx = il.Select((x, i) => (i, x)).First(z => z.x.IsLoopHeader).i;
            Assert.True(il[idx].IsLoopHeader);
            Assert.True(il[idx].IsLeader);
            Assert.Equal("IL_0000: nop ", il.ToString().Split('\r', '\n').First());
        }

        static int GotoMethod(int i)
        {
            goto BEGIN;

        LT0:
            return i * 2;

        GTE0:
            return i / 2;

        BEGIN:
            if (i < 0)
                goto LT0;
            else
                goto GTE0;
        }

        [Fact]
        public static void TestGoto()
        {
            var il = new Func<int, int>(GotoMethod).Method.GetInstructions();
        }
    }
}
