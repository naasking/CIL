using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace CIL.Expressions
{
    public class LinqDecompiler : ExpressionError<Expression>
    {
        public override Expression Add(Expression left, Expression right)
        {
            return Expression.Add(left, right);
        }
        public override Expression AddressGet(Expression address)
        {
            return address;
        }
        public override Expression AddOverflow(Expression left, Expression right)
        {
            return Expression.AddChecked(left, right);
        }
        public override Expression AddressSet(Expression address, Expression value)
        {
            return Expression.Assign(address, value);
        }
        public override Expression And(Expression left, Expression right)
        {
            return Expression.And(left, right);
        }
        public override Expression Array(Type arrayType, IEnumerable<Expression> arrayBounds)
        {
            return Expression.NewArrayInit(arrayType, arrayBounds);
        }
        public override Expression ArrayGet(Expression array, Expression index)
        {
            return Expression.ArrayIndex(array, index);
        }
        public override Expression ArrayLength(Expression exp)
        {
            return Expression.ArrayLength(exp);
        }
        public override Expression ArraySet(Expression array, Expression index, Expression value)
        {
            return Assign(Expression.ArrayAccess(array, index), value);
        }
        public override Expression Assign(Expression variable, Expression value)
        {
            return Expression.Assign(variable, value);
        }
        public override Expression Box(Expression exp)
        {
            return exp;
        }
        public override Expression Property(Expression obj, PropertyInfo property)
        {
            return Expression.Property(obj, property);
        }
        public override Expression Property(Expression obj, PropertyInfo property, IEnumerable<Expression> indexers)
        {
            return Expression.Property(obj, property, indexers);
        }
        //public override Expression Set(Expression obj, MethodBase getter, Expression value)
        //{
        //    Expression.MemberInit()
        //    return Expression.Property(obj, (MethodInfo)setter, );
        //}
        public override Expression Call(MethodBase method, bool tailcall, Expression obj, IEnumerable<Expression> args)
        {
            return method.IsStatic ? Expression.Call((MethodInfo)method, args):
                                     Expression.Call(obj, (MethodInfo)method, args);
        }
        public override Expression Cast(Expression exp, Type castTo)
        {
            return exp.Type == castTo                      ? exp:
                   exp.NodeType == ExpressionType.Constant ? Expression.Constant(Convert.ChangeType((exp as ConstantExpression).Value, castTo)):
                                                             Expression.Convert(exp, castTo) as Expression;
        }
        public override Expression CastOverflow(Expression exp, Type castTo)
        {
            return Expression.ConvertChecked(exp, castTo);
        }
        public override Expression Constant<TValue>(TValue value)
        {
            return Expression.Constant(value);
        }
        public override Expression Divide(Expression left, Expression right)
        {
            return Expression.Divide(left, right);
        }
        public override Expression Equal(Expression lhs, Expression rhs)
        {
            // apply a simple constant-folding
            if (lhs.Type == typeof(bool) && rhs.Type == typeof(bool))
            {
                var lc = lhs as ConstantExpression;
                var rc = rhs as ConstantExpression;
                var e = lc != null && lc.Value.Equals(false) ? rhs:
                        rc != null && rc.Value.Equals(false) ? lhs:
                                                               null;
                if (e != null) return Not(e);
            }
            return Expression.Equal(lhs, rhs);
        }
        public override Expression Field(FieldInfo field, Expression obj)
        {
            return Expression.Field(obj, field);
        }
        //public override Expression Goto(Expression exp)
        //{
        //    return Expression.Goto();
        //}
        public override Expression GreaterThan(Expression lhs, Expression rhs)
        {
            return Expression.GreaterThan(lhs, rhs);
        }
        public override Expression GreaterThanOrEqual(Expression lhs, Expression rhs)
        {
            return Expression.GreaterThanOrEqual(lhs, rhs);
        }
        public override Expression If(Expression cond, Expression _then, Expression _else)
        {
            return Expression.IfThenElse(cond, _then, _else);
        }
        public override Expression If(Expression cond, Expression _then)
        {
            return Expression.IfThen(cond, _then);
        }
        public override Expression LeftShift(Expression value, Expression shift)
        {
            return Expression.LeftShift(value, shift);
        }
        public override Expression LessThan(Expression lhs, Expression rhs)
        {
            return Expression.LessThan(lhs, rhs);
        }
        public override Expression LessThanOrEqual(Expression lhs, Expression rhs)
        {
            return Expression.LessThanOrEqual(lhs, rhs);
        }
        public override Expression Local(LocalVariableInfo x)
        {
            return Expression.Variable(x.LocalType, "_x" + x.LocalIndex);
        }
        public override Expression Modulo(Expression left, Expression right)
        {
            return Expression.Modulo(left, right);
        }
        public override Expression Multiply(Expression left, Expression right)
        {
            return Expression.Multiply(left, right);
        }
        public override Expression MultiplyOverflow(Expression left, Expression right)
        {
            return Expression.MultiplyChecked(left, right);
        }
        public override Expression Negate(Expression exp)
        {
            return Expression.Negate(exp);
        }
        public override Expression New(ConstructorInfo ctor, IEnumerable<Expression> args)
        {
            return Expression.New(ctor, args);
        }
        public override Expression Not(Expression exp)
        {
            return exp.NodeType == ExpressionType.Equal
                 ? NotEqual((exp as BinaryExpression).Left, (exp as BinaryExpression).Right)
                 : Expression.Not(exp);
        }
        public override Expression NotEqual(Expression lhs, Expression rhs)
        {
            return Expression.NotEqual(lhs, rhs);
        }
        public override Expression Or(Expression left, Expression right)
        {
            return Expression.Or(left, right);
        }
        public override Expression Param(ParameterInfo x)
        {
            return Param(x.Name, x.ParameterType);
        }
        public override Expression Param(string name, Type type)
        {
            return Expression.Parameter(type, name);
        }
        public override Expression Rethrow()
        {
            return Expression.Rethrow();
        }
        public override Expression Return(Expression exp)
        {
            //FIXME: not sure what to do with labelled expressions
            //return Expression.Return(exp);
            return exp;
        }
        public override Expression RightShift(Expression value, Expression shift)
        {
            return base.RightShift(value, shift);
        }
        public override Expression SizeOf(Type valueType)
        {
            return Expression.Constant(System.Runtime.InteropServices.Marshal.SizeOf(valueType));
        }
        public override Expression StructCopy(Expression dst, Expression src, Type structType)
        {
            return Expression.Assign(dst, src);
        }
        public override Expression Subtract(Expression left, Expression right)
        {
            return Expression.Subtract(left, right);
        }
        public override Expression SubtractOverflow(Expression left, Expression right)
        {
            return Expression.SubtractChecked(left, right);
        }
        public override Expression Switch<TCase>(Expression value, IEnumerable<KeyValuePair<TCase, Expression>> branches)
        {
            return Expression.Switch(value, branches.Select(x => Expression.SwitchCase(x.Value, Expression.Constant(x.Key))).ToArray());
        }
        public override Expression TryCatchFinally(Expression _try, IEnumerable<KeyValuePair<Type, Expression>> _catch, Expression _finally)
        {
            return Expression.TryCatchFinally(_try, _finally, _catch.Select(x => Expression.Catch(x.Key, x.Value)).ToArray());
        }
        public override Expression TypeAs(Expression exp, Type castTo)
        {
            return Expression.TypeAs(exp, castTo);
        }
        public override Expression Throw(Expression exp)
        {
            return Expression.Throw(exp);
        }
        public override Expression Unbox(Expression value, Type type)
        {
            return Expression.Unbox(value, type);
        }
        public override Expression Xor(Expression left, Expression right)
        {
            return Expression.ExclusiveOr(left, right);
        }

        public override Type TypeOf(Expression exp)
        {
            return exp.Type;
        }
        public override bool IsConstant(Expression exp)
        {
            return exp is ConstantExpression;
        }
        public override object Value(Expression exp)
        {
            var c = exp as ConstantExpression;
            if (c == null) throw new InvalidCastException("Expression is not a constant.");
            return c.Value;
        }

        public static Expression<T> Decompile<T>(T func)
            where T : class
        {
            if (func == null) throw new ArgumentNullException("func");
            var f = func as Delegate;
            var eval = new Stack<Expression>();
            Expression[] args;
            var dec = new LinqDecompiler();
            var body = dec.Cast(f.Method.Decompile(dec, out args), f.Method.ReturnType);
            return Expression.Lambda<T>(body, (f.Method.IsStatic ? args : args.Skip(1)).Cast<ParameterExpression>());
        }
    }
}
