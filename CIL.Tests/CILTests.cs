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
        public void TestSimple()
        {
            Func<int> foo = () => 3;
            Expression<Func<int>> fooe = () => 3;
            Assert.Equal(fooe.ToString(), foo.GetExpression().ToString());
            Assert.NotEqual(Expression.Constant(3).ToString(), Expression.Constant(0).ToString());
        }
    }
}
