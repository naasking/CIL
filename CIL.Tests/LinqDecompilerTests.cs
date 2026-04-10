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
    /// Comprehensive tests for every LinqDecompiler expression method and
    /// end-to-end decompilation of delegates to LINQ expression trees.
    /// </summary>
    public class LinqDecompilerTests
    {
        static readonly LinqDecompiler dec = new LinqDecompiler();

        // Reusable typed parameter expressions
        static readonly Expression intX = Expression.Parameter(typeof(int), "x");
        static readonly Expression intY = Expression.Parameter(typeof(int), "y");
        static readonly Expression strS = Expression.Parameter(typeof(string), "s");
        static readonly Expression boolB = Expression.Parameter(typeof(bool), "b");
        static readonly Expression objO = Expression.Parameter(typeof(object), "o");

        #region Arithmetic operations

        [Fact]
        public void Add_ReturnsBinaryAdd()
        {
            var result = dec.Add(intX, intY);
            Assert.Equal(ExpressionType.Add, result.NodeType);
            Assert.Equal(typeof(int), result.Type);
        }

        [Fact]
        public void AddOverflow_ReturnsAddChecked()
        {
            var result = dec.AddOverflow(intX, intY);
            Assert.Equal(ExpressionType.AddChecked, result.NodeType);
        }

        [Fact]
        public void Subtract_ReturnsBinarySubtract()
        {
            var result = dec.Subtract(intX, intY);
            Assert.Equal(ExpressionType.Subtract, result.NodeType);
        }

        [Fact]
        public void SubtractOverflow_ReturnsSubtractChecked()
        {
            var result = dec.SubtractOverflow(intX, intY);
            Assert.Equal(ExpressionType.SubtractChecked, result.NodeType);
        }

        [Fact]
        public void Multiply_ReturnsBinaryMultiply()
        {
            var result = dec.Multiply(intX, intY);
            Assert.Equal(ExpressionType.Multiply, result.NodeType);
        }

        [Fact]
        public void MultiplyOverflow_ReturnsMultiplyChecked()
        {
            var result = dec.MultiplyOverflow(intX, intY);
            Assert.Equal(ExpressionType.MultiplyChecked, result.NodeType);
        }

        [Fact]
        public void Divide_ReturnsBinaryDivide()
        {
            var result = dec.Divide(intX, intY);
            Assert.Equal(ExpressionType.Divide, result.NodeType);
        }

        [Fact]
        public void Modulo_ReturnsBinaryModulo()
        {
            var result = dec.Modulo(intX, intY);
            Assert.Equal(ExpressionType.Modulo, result.NodeType);
        }

        [Fact]
        public void Negate_ReturnsUnaryNegate()
        {
            var result = dec.Negate(intX);
            Assert.Equal(ExpressionType.Negate, result.NodeType);
        }

        #endregion

        #region Bitwise and logical operations

        [Fact]
        public void And_ReturnsBinaryAnd()
        {
            var result = dec.And(intX, intY);
            Assert.Equal(ExpressionType.And, result.NodeType);
        }

        [Fact]
        public void Or_ReturnsBinaryOr()
        {
            var result = dec.Or(intX, intY);
            Assert.Equal(ExpressionType.Or, result.NodeType);
        }

        [Fact]
        public void Xor_ReturnsExclusiveOr()
        {
            var result = dec.Xor(intX, intY);
            Assert.Equal(ExpressionType.ExclusiveOr, result.NodeType);
        }

        [Fact]
        public void Not_Bool_ReturnsNot()
        {
            var result = dec.Not(boolB);
            Assert.Equal(ExpressionType.Not, result.NodeType);
            Assert.Equal(typeof(bool), result.Type);
        }

        [Fact]
        public void Not_OfEqualExpr_ReturnsNotEqual()
        {
            var eq = Expression.Equal(intX, intY);
            var result = dec.Not(eq);
            Assert.Equal(ExpressionType.NotEqual, result.NodeType);
        }

        [Fact]
        public void Not_OfNonBool_ThrowsNoCoercion()
        {
            // int→bool coercion doesn't exist in LINQ expressions.
            // This path never triggers in practice: CIL uses int for bools,
            // and the Process interpreter resolves the type before calling Not.
            Assert.ThrowsAny<Exception>(() => dec.Not(intX));
        }

        [Fact]
        public void LeftShift_ReturnsLeftShift()
        {
            var result = dec.LeftShift(intX, intY);
            Assert.Equal(ExpressionType.LeftShift, result.NodeType);
        }

        [Fact]
        public void RightShift_ReturnsRightShift()
        {
            var result = dec.RightShift(intX, intY);
            Assert.Equal(ExpressionType.RightShift, result.NodeType);
        }

        #endregion

        #region Comparison operations

        [Fact]
        public void Equal_ReturnsBinaryEqual()
        {
            var result = dec.Equal(intX, intY);
            Assert.Equal(ExpressionType.Equal, result.NodeType);
        }

        [Fact]
        public void Equal_BoolFalseOnLeft_FoldsToNot()
        {
            var result = dec.Equal(Expression.Constant(false), boolB);
            Assert.Equal(ExpressionType.Not, result.NodeType);
        }

        [Fact]
        public void Equal_BoolFalseOnRight_FoldsToNot()
        {
            var result = dec.Equal(boolB, Expression.Constant(false));
            Assert.Equal(ExpressionType.Not, result.NodeType);
        }

        [Fact]
        public void NotEqual_ReturnsBinaryNotEqual()
        {
            var result = dec.NotEqual(intX, intY);
            Assert.Equal(ExpressionType.NotEqual, result.NodeType);
        }

        [Fact]
        public void GreaterThan_ReturnsGreaterThan()
        {
            var result = dec.GreaterThan(intX, intY);
            Assert.Equal(ExpressionType.GreaterThan, result.NodeType);
        }

        [Fact]
        public void GreaterThanOrEqual_ReturnsGreaterThanOrEqual()
        {
            var result = dec.GreaterThanOrEqual(intX, intY);
            Assert.Equal(ExpressionType.GreaterThanOrEqual, result.NodeType);
        }

        [Fact]
        public void LessThan_ReturnsLessThan()
        {
            var result = dec.LessThan(intX, intY);
            Assert.Equal(ExpressionType.LessThan, result.NodeType);
        }

        [Fact]
        public void LessThanOrEqual_ReturnsLessThanOrEqual()
        {
            var result = dec.LessThanOrEqual(intX, intY);
            Assert.Equal(ExpressionType.LessThanOrEqual, result.NodeType);
        }

        #endregion

        #region Constants and type info extraction

        [Fact]
        public void Constant_Int()
        {
            var result = dec.Constant(42);
            Assert.IsType<ConstantExpression>(result);
            Assert.Equal(42, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void Constant_String()
        {
            var result = dec.Constant("hello");
            Assert.IsType<ConstantExpression>(result);
            Assert.Equal("hello", ((ConstantExpression)result).Value);
        }

        [Fact]
        public void Constant_Double()
        {
            var result = dec.Constant(3.14);
            Assert.IsType<ConstantExpression>(result);
            Assert.Equal(3.14, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void Constant_NullWithType()
        {
            var result = dec.Constant(null, typeof(string));
            var constant = Assert.IsAssignableFrom<ConstantExpression>(result);
            Assert.Null(constant.Value);
            Assert.Equal(typeof(string), result.Type);
        }

        [Fact]
        public void TypeOf_ReturnsCorrectType()
        {
            Assert.Equal(typeof(int), dec.TypeOf(intX));
            Assert.Equal(typeof(string), dec.TypeOf(strS));
            Assert.Equal(typeof(bool), dec.TypeOf(boolB));
        }

        [Fact]
        public void IsConstant_TrueForConstant()
        {
            Assert.True(dec.IsConstant(Expression.Constant(1)));
        }

        [Fact]
        public void IsConstant_FalseForParameter()
        {
            Assert.False(dec.IsConstant(intX));
        }

        [Fact]
        public void Value_ReturnsConstantValue()
        {
            Assert.Equal(42, dec.Value(Expression.Constant(42)));
            Assert.Equal("hi", dec.Value(Expression.Constant("hi")));
        }

        [Fact]
        public void Value_ThrowsForNonConstant()
        {
            Assert.Throws<InvalidCastException>(() => dec.Value(intX));
        }

        #endregion

        #region Type operations

        [Fact]
        public void Cast_SameType_ReturnsSameExpression()
        {
            var result = dec.Cast(intX, typeof(int));
            Assert.Same(intX, result);
        }

        [Fact]
        public void Cast_DifferentType_ReturnsConvert()
        {
            var result = dec.Cast(intX, typeof(long));
            Assert.Equal(ExpressionType.Convert, result.NodeType);
            Assert.Equal(typeof(long), result.Type);
        }

        [Fact]
        public void Cast_Constant_FoldsToNewConstant()
        {
            var result = dec.Cast(Expression.Constant(3), typeof(long));
            Assert.IsType<ConstantExpression>(result);
            Assert.Equal(3L, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void CastOverflow_ReturnsConvertChecked()
        {
            var result = dec.CastOverflow(intX, typeof(short));
            Assert.Equal(ExpressionType.ConvertChecked, result.NodeType);
            Assert.Equal(typeof(short), result.Type);
        }

        [Fact]
        public void TypeAs_ReturnsTypeAsExpression()
        {
            var result = dec.TypeAs(objO, typeof(string));
            Assert.Equal(ExpressionType.TypeAs, result.NodeType);
            Assert.Equal(typeof(string), result.Type);
        }

        [Fact]
        public void Box_ReturnsOriginal()
        {
            var result = dec.Box(intX);
            Assert.Same(intX, result);
        }

        [Fact]
        public void Unbox_ReturnsUnboxExpression()
        {
            var result = dec.Unbox(objO, typeof(int));
            Assert.Equal(ExpressionType.Unbox, result.NodeType);
            Assert.Equal(typeof(int), result.Type);
        }

        #endregion

        #region Parameters and locals

        [Fact]
        public void Param_FromNameAndType()
        {
            var result = dec.Param("foo", typeof(double));
            Assert.Equal(ExpressionType.Parameter, result.NodeType);
            Assert.Equal(typeof(double), result.Type);
            Assert.Equal("foo", ((ParameterExpression)result).Name);
        }

        [Fact]
        public void Param_FromParameterInfo()
        {
            var method = new Func<int, int>(z => z).Method;
            var param = method.GetParameters()[0];
            var result = dec.Param(param);
            Assert.Equal(ExpressionType.Parameter, result.NodeType);
            Assert.Equal(param.ParameterType, result.Type);
        }

        [Fact]
        public void Local_CreatesNamedVariable()
        {
            var method = new Func<int>(() => { var z = 42; return z; }).Method;
            var locals = method.GetMethodBody()?.LocalVariables;
            Assert.NotNull(locals);
            Assert.NotEmpty(locals);
            var result = dec.Local(locals[0]);
            Assert.Equal(ExpressionType.Parameter, result.NodeType); // Variable is a Parameter subtype
            Assert.StartsWith("_x", ((ParameterExpression)result).Name);
        }

        #endregion

        #region Control flow

        [Fact]
        public void If_ThenElse_ReturnsConditional()
        {
            var result = dec.If(boolB, intX, intY);
            Assert.Equal(ExpressionType.Conditional, result.NodeType);
            var cond = (ConditionalExpression)result;
            Assert.Same(boolB, cond.Test);
            Assert.Same(intX, cond.IfTrue);
            Assert.Same(intY, cond.IfFalse);
        }

        [Fact]
        public void If_ThenOnly_ReturnsConditional()
        {
            var body = Expression.Empty();
            var result = dec.If(boolB, body);
            Assert.Equal(ExpressionType.Conditional, result.NodeType);
        }

        [Fact]
        public void Switch_ReturnsSwitchExpression()
        {
            // Switch cases must be void or provide a default body
            var cases = new[]
            {
                new KeyValuePair<int, Expression>(0, Expression.Empty()),
                new KeyValuePair<int, Expression>(1, Expression.Empty()),
            };
            var result = dec.Switch(intX, cases);
            Assert.Equal(ExpressionType.Switch, result.NodeType);
            var sw = (SwitchExpression)result;
            Assert.Equal(2, sw.Cases.Count);
        }

        [Fact]
        public void Loop_ReturnsLoopExpression()
        {
            var body = Expression.Constant(0);
            var exitCond = Expression.Constant(false);
            var exitValue = Expression.Constant(42);
            var result = dec.Loop(body, exitCond, exitValue);
            Assert.Equal(ExpressionType.Loop, result.NodeType);
        }

        [Fact]
        public void Block_ReturnsBlockExpression()
        {
            var exprs = new[] { Expression.Constant(1), Expression.Constant(2) };
            var result = dec.Block(exprs);
            Assert.Equal(ExpressionType.Block, result.NodeType);
        }

        #endregion

        #region Method calls

        [Fact]
        public void Call_StaticMethod()
        {
            var method = typeof(int).GetMethod("Parse", new[] { typeof(string) });
            var result = dec.Call(method, false, null, new[] { Expression.Constant("42") });
            Assert.Equal(ExpressionType.Call, result.NodeType);
            Assert.Equal(method, ((MethodCallExpression)result).Method);
        }

        [Fact]
        public void Call_InstanceMethod()
        {
            var method = typeof(string).GetMethod("Substring", new[] { typeof(int) });
            var result = dec.Call(method, false, strS, new[] { Expression.Constant(1) });
            Assert.Equal(ExpressionType.Call, result.NodeType);
            Assert.Equal(method, ((MethodCallExpression)result).Method);
            Assert.Same(strS, ((MethodCallExpression)result).Object);
        }

        #endregion

        #region Properties and fields

        [Fact]
        public void Property_ReturnsPropertyAccess()
        {
            var prop = typeof(string).GetProperty("Length");
            var result = dec.Property(strS, prop);
            Assert.Equal(ExpressionType.MemberAccess, result.NodeType);
            Assert.Equal(typeof(int), result.Type);
        }

        [Fact]
        public void Property_Indexed()
        {
            var prop = typeof(List<int>).GetProperty("Item");
            var list = Expression.Parameter(typeof(List<int>), "list");
            var result = dec.Property(list, prop, new[] { Expression.Constant(0) });
            Assert.Equal(ExpressionType.Index, result.NodeType);
        }

        [Fact]
        public void Field_ReturnsFieldAccess()
        {
            var field = typeof(string).GetField("Empty", BindingFlags.Static | BindingFlags.Public);
            var result = dec.Field(field, null);
            Assert.Equal(ExpressionType.MemberAccess, result.NodeType);
        }

        #endregion

        #region Assignment and address operations

        [Fact]
        public void Assign_SameType()
        {
            var v = Expression.Variable(typeof(int), "v");
            var result = dec.Assign(v, Expression.Constant(5));
            Assert.Equal(ExpressionType.Assign, result.NodeType);
            var assign = (BinaryExpression)result;
            Assert.Same(v, assign.Left);
        }

        [Fact]
        public void Assign_DifferentType_InsertsConvert()
        {
            var v = Expression.Variable(typeof(long), "v");
            var result = dec.Assign(v, Expression.Constant(5));
            var assign = (BinaryExpression)result;
            Assert.Equal(ExpressionType.Convert, assign.Right.NodeType);
            Assert.Equal(typeof(long), assign.Right.Type);
        }

        [Fact]
        public void AddressOf_ReturnsOriginal()
        {
            var result = dec.AddressOf(intX);
            Assert.Same(intX, result);
        }

        [Fact]
        public void AddressGet_ReturnsOriginal()
        {
            var result = dec.AddressGet(intX);
            Assert.Same(intX, result);
        }

        [Fact]
        public void AddressSet_ReturnsAssign()
        {
            var v = Expression.Variable(typeof(int), "ptr");
            var result = dec.AddressSet(v, Expression.Constant(5));
            Assert.Equal(ExpressionType.Assign, result.NodeType);
        }

        #endregion

        #region Object and struct operations

        [Fact]
        public void New_ReturnsNewExpression()
        {
            var ctor = typeof(Exception).GetConstructor(new[] { typeof(string) });
            var result = dec.New(ctor, new[] { Expression.Constant("msg") });
            Assert.Equal(ExpressionType.New, result.NodeType);
        }

        [Fact]
        public void ObjectInit_AssignsDefault()
        {
            var v = Expression.Variable(typeof(int), "v");
            var result = dec.ObjectInit(v, typeof(int));
            Assert.Equal(ExpressionType.Assign, result.NodeType);
            var assign = (BinaryExpression)result;
            Assert.Equal(ExpressionType.Default, assign.Right.NodeType);
            Assert.Equal(typeof(int), assign.Right.Type);
        }

        [Fact]
        public void ObjectInit_ReferenceType()
        {
            var v = Expression.Variable(typeof(string), "v");
            var result = dec.ObjectInit(v, typeof(string));
            var assign = (BinaryExpression)result;
            Assert.Equal(ExpressionType.Default, assign.Right.NodeType);
            Assert.Equal(typeof(string), assign.Right.Type);
        }

        [Fact]
        public void StructCopy_ReturnsAssign()
        {
            var dst = Expression.Variable(typeof(int), "dst");
            var src = Expression.Variable(typeof(int), "src");
            var result = dec.StructCopy(dst, src, typeof(int));
            Assert.Equal(ExpressionType.Assign, result.NodeType);
        }

        [Fact]
        public void SizeOf_ReturnsCorrectSize()
        {
            var result = dec.SizeOf(typeof(int));
            Assert.IsType<ConstantExpression>(result);
            Assert.Equal(4, ((ConstantExpression)result).Value);
        }

        #endregion

        #region Array operations

        [Fact]
        public void Array_ReturnsNewArrayInit()
        {
            var result = dec.Array(typeof(int), new[] { Expression.Constant(5) });
            Assert.Equal(ExpressionType.NewArrayInit, result.NodeType);
            Assert.Equal(typeof(int[]), result.Type);
        }

        [Fact]
        public void ArrayGet_ReturnsArrayIndex()
        {
            var arr = Expression.Parameter(typeof(int[]), "arr");
            var result = dec.ArrayGet(arr, Expression.Constant(0));
            Assert.Equal(ExpressionType.ArrayIndex, result.NodeType);
            Assert.Equal(typeof(int), result.Type);
        }

        [Fact]
        public void ArrayLength_ReturnsArrayLength()
        {
            var arr = Expression.Parameter(typeof(int[]), "arr");
            var result = dec.ArrayLength(arr);
            Assert.Equal(ExpressionType.ArrayLength, result.NodeType);
        }

        [Fact]
        public void ArraySet_ReturnsAssignToArrayAccess()
        {
            var arr = Expression.Parameter(typeof(int[]), "arr");
            var result = dec.ArraySet(arr, Expression.Constant(0), Expression.Constant(42));
            Assert.Equal(ExpressionType.Assign, result.NodeType);
        }

        #endregion

        #region Exception handling

        [Fact]
        public void Throw_ReturnsThrowExpression()
        {
            var ex = Expression.New(typeof(Exception));
            var result = dec.Throw(ex);
            Assert.Equal(ExpressionType.Throw, result.NodeType);
        }

        [Fact]
        public void Rethrow_ReturnsThrowExpression()
        {
            var result = dec.Rethrow();
            Assert.Equal(ExpressionType.Throw, result.NodeType);
        }

        [Fact]
        public void TryCatchFinally_SingleCatch()
        {
            var tryBody = Expression.Constant(1);
            var catches = new[]
            {
                new KeyValuePair<Type, Expression>(typeof(InvalidOperationException), Expression.Constant(2)),
            };
            var finallyBody = Expression.Empty();
            var result = dec.TryCatchFinally(tryBody, catches, finallyBody);

            Assert.Equal(ExpressionType.Try, result.NodeType);
            var tryExpr = (TryExpression)result;
            Assert.Single(tryExpr.Handlers);
            Assert.Equal(typeof(InvalidOperationException), tryExpr.Handlers[0].Test);
            Assert.NotNull(tryExpr.Finally);
        }

        [Fact]
        public void TryCatchFinally_MultipleCatches()
        {
            var tryBody = Expression.Constant(1);
            var catches = new[]
            {
                new KeyValuePair<Type, Expression>(typeof(FormatException), Expression.Constant(-1)),
                new KeyValuePair<Type, Expression>(typeof(OverflowException), Expression.Constant(-2)),
            };
            var finallyBody = Expression.Empty();
            var result = dec.TryCatchFinally(tryBody, catches, finallyBody);

            var tryExpr = (TryExpression)result;
            Assert.Equal(2, tryExpr.Handlers.Count);
            Assert.Equal(typeof(FormatException), tryExpr.Handlers[0].Test);
            Assert.Equal(typeof(OverflowException), tryExpr.Handlers[1].Test);
        }

        [Fact]
        public void EndFilter_ReturnsOriginal()
        {
            var result = dec.EndFilter(boolB);
            Assert.Same(boolB, result);
        }

        #endregion

        #region Pass-through and identity operations

        [Fact]
        public void Duplicate_ReturnsOriginal()
        {
            var result = dec.Duplicate(intX);
            Assert.Same(intX, result);
        }

        [Fact]
        public void Goto_ReturnsOriginal()
        {
            var result = dec.Goto(intX);
            Assert.Same(intX, result);
        }

        [Fact]
        public void Return_ReturnsOriginal()
        {
            var result = dec.Return(intX);
            Assert.Same(intX, result);
        }

        [Fact]
        public void Unaligned_ReturnsOriginal()
        {
            var result = dec.Unaligned(intX);
            Assert.Same(intX, result);
        }

        #endregion

        #region Unsupported operations throw NotSupportedException

        [Fact]
        public void CallIndirect_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => dec.CallIndirect(new byte[0], intX, false, Enumerable.Empty<Expression>()));
        }

        [Fact]
        public void GetPointer_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => dec.GetPointer(typeof(object).GetMethod("ToString"), intX));
        }

        [Fact]
        public void Jump_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => dec.Jump(typeof(object).GetMethod("ToString")));
        }

        [Fact]
        public void LocalAlloc_Throws()
        {
            Assert.Throws<NotSupportedException>(() => dec.LocalAlloc(intX));
        }

        [Fact]
        public void ArgumentList_Throws()
        {
            Assert.Throws<NotSupportedException>(() => dec.ArgumentList());
        }

        [Fact]
        public void BlockInit_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => dec.BlockInit(intX, intY, Expression.Constant(10)));
        }

        [Fact]
        public void BlockCopy_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => dec.BlockCopy(intX, intY, Expression.Constant(10)));
        }

        #endregion

        #region End-to-end decompilation

        [Fact]
        public void Decompile_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => LinqDecompiler.Decompile<Func<int>>(null));
        }

        [Fact]
        public void Decompile_ConstantReturn()
        {
            Func<int> foo = () => 3;
            Expression<Func<int>> expected = () => 3;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_ParameterInequality()
        {
            Func<int, bool> foo = x => x != 3;
            Expression<Func<int, bool>> expected = x => x != 3;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_MethodChain()
        {
            Func<string, string> foo = x => x.Substring(3).Replace(';', ':').PadRight(3);
            Expression<Func<string, string>> expected = x => x.Substring(3).Replace(';', ':').PadRight(3);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_StringConcat()
        {
            Func<string, string> foo = x => string.Concat(x, "!");
            Expression<Func<string, string>> expected = x => string.Concat(x, "!");
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_StaticMethodCall()
        {
            Func<double, double> foo = x => Math.Abs(x);
            Expression<Func<double, double>> expected = x => Math.Abs(x);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
        }

        [Fact]
        public void Decompile_ReturnsLambdaExpression()
        {
            Func<int, int> foo = x => x;
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.NotNull(decompiled);
            Assert.Single(decompiled.Parameters);
            Assert.Equal(decompiled.Parameters[0], decompiled.Body);
            Assert.Equal(typeof(int), decompiled.ReturnType);
        }

        [Fact]
        public void Decompile_TwoParameterMethod()
        {
            Func<string, string, string> foo = (a, b) => string.Concat(a, b);
            Expression<Func<string, string, string>> expected = (a, b) => string.Concat(a, b);
            var decompiled = LinqDecompiler.Decompile(foo);
            Assert.Equal(expected.ToString(), decompiled.ToString());
            Assert.Equal(2, decompiled.Parameters.Count);
        }

        #endregion
    }
}
