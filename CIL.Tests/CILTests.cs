using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Xunit;
using CIL.Expressions;

namespace CIL.Tests
{
    public class CILTests
    {
        [Fact]
        public static void TestSimple()
        {
            Func<int> foo = () => 3;
            Expression<Func<int>> fooe = () => 3;
            var decompiled = foo.GetExpression();
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }
        [Fact]
        public static void TestEqConst()
        {
            Func<bool> foo = () => 3 != 5;
            Expression<Func<bool>> fooe = () => true;
            var decompiled = foo.GetExpression();
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }

        [Fact]
        public static void TestEqParam()
        {
            Func<int, bool> foo = x => 3 != x;
            Expression<Func<int, bool>> fooe = x => 3 != x;
            var decompiled = foo.GetExpression();
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }

        [Fact]
        public static void TestEqParamReverse()
        {
            Func<int, bool> foo = x => x != 3;
            Expression<Func<int, bool>> fooe = x => x != 3;
            var decompiled = foo.GetExpression();
            Assert.Equal(fooe.ToString(), decompiled.ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), decompiled.ToString());
        }

        public static void Main(string[] args)
        {
            TestSimple();
            TestEqConst();
            TestEqParam();
        }
    }
}
