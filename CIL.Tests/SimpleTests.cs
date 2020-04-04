using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public static void MainOrig(string[] args)
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
                    i -= 3;
                if (i > 100)
                    break;
                --i;
            }
            return i;
        }

        [Fact]
        static void TestLoop()
        {
            var il = new Func<int, int>(LoopMethod).Method.GetInstructions();
        }
    }
}
