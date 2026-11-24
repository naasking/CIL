using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CIL.Expressions
{
    public class LinqDecompiler : ExpressionError<Expression>
    {
        LabelTarget returnTarget;
        int returnCount;

        public LinqDecompiler()
        {
        }

        LinqDecompiler(Type returnType)
        {
            returnTarget = Expression.Label(returnType, "_ret");
        }

        public override Expression Add(Expression left, Expression right) =>
            Expression.Add(left, right);

        public override Expression AddressGet(Expression address) =>
            address;

        public override Expression AddOverflow(Expression left, Expression right) =>
            Expression.AddChecked(left, right);

        public override Expression AddressSet(Expression address, Expression value) =>
            Assign(address, value);

        public override Expression And(Expression left, Expression right) =>
            Expression.And(left, right);

        public override Expression AndAlso(Expression left, Expression right) =>
            Expression.AndAlso(left, right);

        public override Expression Array(Type arrayType, IEnumerable<Expression> arrayBounds) =>
            Expression.NewArrayBounds(arrayType, arrayBounds);

        public override Expression ArrayGet(Expression array, Expression index) =>
            Expression.ArrayIndex(array, index);

        public override Expression ArrayLength(Expression exp) =>
            Expression.ArrayLength(exp);

        public override Expression ArraySet(Expression array, Expression index, Expression value) =>
            Assign(Expression.ArrayAccess(array, index), value);

        public override Expression Assign(Expression variable, Expression value) =>
            variable.Type != value.Type
                ? Expression.Assign(variable, Expression.Convert(value, variable.Type))
                : Expression.Assign(variable, value);

        public override Expression Box(Expression exp, Type type)
        {
            var coerced = Cast(exp, type);
            return coerced.Type == typeof(object) ? coerced : Expression.Convert(coerced, typeof(object));
        }

        public override Expression Property(Expression obj, PropertyInfo property) =>
            Expression.Property(obj, property);

        public override Expression Property(Expression obj, PropertyInfo property, IEnumerable<Expression> indexers) =>
            Expression.Property(obj, property, indexers);

        public override Expression Call(MethodBase method, bool tailcall, Expression obj, IEnumerable<Expression> args) =>
            method.IsStatic
                ? Expression.Call((MethodInfo)method, args)
                : Expression.Call(obj, (MethodInfo)method, args);

        public override Expression Cast(Expression exp, Type castTo)
        {
            if (exp.Type == castTo || castTo == typeof(void))
                return exp;
            if (exp is ConstantExpression c && TryConvertConstant(c.Value, castTo, out var value))
                return Expression.Constant(value, castTo);
            return Expression.Convert(exp, castTo);
        }

        static bool TryConvertConstant(object value, Type castTo, out object result)
        {
            result = null;
            if (value == null)
                return !castTo.IsValueType || Nullable.GetUnderlyingType(castTo) != null;
            try
            {
                result = castTo.IsEnum                  ? Enum.ToObject(castTo, value) :
                         castTo.IsInstanceOfType(value) ? value :
                                                          Convert.ChangeType(value, castTo);
                return true;
            }
            catch (Exception e) when (e is InvalidCastException || e is OverflowException || e is FormatException || e is ArgumentException)
            {
                return false;
            }
        }

        public override Expression CastOverflow(Expression exp, Type castTo) =>
            Expression.ConvertChecked(exp, castTo);

        public override Expression Constant<TValue>(TValue value) =>
            Expression.Constant(value);

        public override Expression Constant(object value, Type type) =>
            Expression.Constant(value, type);

        public override Expression Divide(Expression left, Expression right) =>
            Expression.Divide(left, right);

        public override Expression Equal(Expression lhs, Expression rhs)
        {
            // apply simple constant-folding so x != y and !(x < y) stay readable
            if (lhs.Type == typeof(bool) && rhs.Type == typeof(bool))
            {
                var lc = lhs as ConstantExpression;
                var rc = rhs as ConstantExpression;
                var e = lc != null && Equals(lc.Value, false) ? rhs :
                        rc != null && Equals(rc.Value, false) ? lhs :
                                                                null;
                if (e != null) return Not(e);
            }
            return Expression.Equal(lhs, rhs);
        }

        public override Expression Field(FieldInfo field, Expression obj) =>
            Expression.Field(obj, field);

        public override Expression Label(string name) =>
            Expression.Label(Expression.Label(name));

        public override Expression MarkLabel(Expression label) =>
            label;

        public override Expression Goto(Expression label) =>
            Expression.Goto(((LabelExpression)label).Target);

        public override Expression Break(Expression label) =>
            Expression.Break(((LabelExpression)label).Target);

        public override Expression Continue(Expression label) =>
            Expression.Continue(((LabelExpression)label).Target);

        public override Expression Loop(Expression body, Expression breakLabel, Expression continueLabel)
        {
            var brk = breakLabel is LabelExpression b ? b.Target : null;
            var cont = continueLabel is LabelExpression c ? c.Target : null;
            return Expression.Loop(body, brk, cont);
        }

        public override Expression GreaterThan(Expression lhs, Expression rhs) =>
            Expression.GreaterThan(lhs, rhs);

        public override Expression GreaterThanOrEqual(Expression lhs, Expression rhs) =>
            Expression.GreaterThanOrEqual(lhs, rhs);

        public override Expression If(Expression cond, Expression _then, Expression _else)
        {
            if (_then.Type == _else.Type)
                return Expression.Condition(cond, _then, _else);
            var type = CommonType(new[] { _then.Type, _else.Type });
            return Expression.Condition(cond, Fit(_then, type), Fit(_else, type), type);
        }

        /// <summary>
        /// The type of a conditional whose branches have the given types: a branch of type
        /// void (a throw, return, or goto) adopts the other type, related reference types use
        /// the more general one, and anything else makes the conditional a void statement.
        /// </summary>
        static Type CommonType(IEnumerable<Type> types)
        {
            var values = types.Where(t => t != typeof(void)).Distinct().ToList();
            if (values.Count == 0)
                return typeof(void);
            if (values.Count == 1)
                return values[0];
            if (values.All(t => !t.IsValueType))
                return values.FirstOrDefault(t => values.All(t.IsAssignableFrom)) ?? typeof(void);
            return typeof(void);
        }

        static Expression Fit(Expression e, Type type)
        {
            return e.Type == type || type == typeof(void) ? e :
                   e.Type == typeof(void)                 ? Expression.Block(e, Expression.Default(type)) :
                                                            (Expression)Expression.Convert(e, type);
        }

        public override Expression If(Expression cond, Expression _then) =>
            Expression.IfThen(cond, _then);

        public override Expression LeftShift(Expression value, Expression shift) =>
            Expression.LeftShift(value, shift);

        public override Expression LessThan(Expression lhs, Expression rhs) =>
            Expression.LessThan(lhs, rhs);

        public override Expression LessThanOrEqual(Expression lhs, Expression rhs) =>
            Expression.LessThanOrEqual(lhs, rhs);

        public override Expression Local(LocalVariableInfo x) =>
            Expression.Variable(x.LocalType, "_x" + x.LocalIndex);

        public override Expression Modulo(Expression left, Expression right) =>
            Expression.Modulo(left, right);

        public override Expression Multiply(Expression left, Expression right) =>
            Expression.Multiply(left, right);

        public override Expression MultiplyOverflow(Expression left, Expression right) =>
            Expression.MultiplyChecked(left, right);

        public override Expression Negate(Expression exp) =>
            Expression.Negate(exp);

        public override Expression New(ConstructorInfo ctor, IEnumerable<Expression> args) =>
            Expression.New(ctor, args);

        public override Expression Not(Expression exp)
        {
            // logical negation for booleans, bitwise complement for integers
            if (exp.Type == typeof(bool))
            {
                switch (exp.NodeType)
                {
                    case ExpressionType.Not:
                        return ((UnaryExpression)exp).Operand;
                    case ExpressionType.Equal:
                        return NotEqual(((BinaryExpression)exp).Left, ((BinaryExpression)exp).Right);
                    case ExpressionType.NotEqual:
                        return Expression.Equal(((BinaryExpression)exp).Left, ((BinaryExpression)exp).Right);
                }
            }
            return Expression.Not(exp);
        }

        public override Expression NotEqual(Expression lhs, Expression rhs) =>
            Expression.NotEqual(lhs, rhs);

        public override Expression Or(Expression left, Expression right) =>
            Expression.Or(left, right);

        public override Expression OrElse(Expression left, Expression right) =>
            Expression.OrElse(left, right);

        public override Expression Param(ParameterInfo x) =>
            Param(x.Name, x.ParameterType);

        public override Expression Param(string name, Type type) =>
            Expression.Parameter(type, name);

        public override Expression Rethrow() =>
            Expression.Rethrow();

        public override Expression Return()
        {
            if (returnTarget == null)
                returnTarget = Expression.Label(typeof(void), "_ret");
            returnCount++;
            return Expression.Return(returnTarget);
        }

        public override Expression Return(Expression exp)
        {
            if (returnTarget == null)
                returnTarget = Expression.Label(exp.Type, "_ret");
            returnCount++;
            return returnTarget.Type == typeof(void)
                ? Expression.Block(exp, Expression.Return(returnTarget))
                : (Expression)Expression.Return(returnTarget, Fit(exp, returnTarget.Type));
        }

        public override Expression Block(IEnumerable<Expression> exp)
        {
            var list = exp is IList<Expression> l ? l : exp.ToList();
            return list.Count == 0 ? Expression.Empty() :
                   list.Count == 1 ? list[0] :
                                     (Expression)Expression.Block(list);
        }

        public override Expression RightShift(Expression value, Expression shift) =>
            Expression.RightShift(value, shift);

        public override Expression SizeOf(Type valueType)
        {
            if (valueType.ContainsGenericParameters)
                throw new NotSupportedException("sizeof an open generic type is not supported.");
            var dm = new System.Reflection.Emit.DynamicMethod("SizeOf", typeof(int), Type.EmptyTypes, typeof(LinqDecompiler).Module, true);
            var il = dm.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Sizeof, valueType);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            return Expression.Constant((int)dm.Invoke(null, null));
        }

        public override Expression StructCopy(Expression dst, Expression src, Type structType) =>
            Expression.Assign(dst, src);

        public override Expression Subtract(Expression left, Expression right) =>
            Expression.Subtract(left, right);

        public override Expression SubtractOverflow(Expression left, Expression right) =>
            Expression.SubtractChecked(left, right);

        public override Expression Switch<TCase>(Expression value, IEnumerable<KeyValuePair<TCase, Expression>> branches, Expression defaultBranch)
        {
            // cases that share a body become a single SwitchCase with several test values
            var groups = branches.GroupBy(x => x.Value).ToList();
            var type = CommonType(groups.Select(g => g.Key.Type).Concat(new[] { defaultBranch.Type }));
            var cases = groups.Select(g => Expression.SwitchCase(Fit(g.Key, type), g.Select(x => Cast(Expression.Constant(x.Key), value.Type)))).ToArray();
            return Expression.Switch(type, value, Fit(defaultBranch, type), null, cases);
        }

        public override Expression TryCatchFinally(Expression _try, IEnumerable<CatchHandler<Expression>> handlers, Expression _finally)
        {
            var catches = handlers.Select(h => Expression.MakeCatchBlock(h.ExceptionType, (ParameterExpression)h.Exception, h.Body, h.HasFilter ? h.Filter : null)).ToArray();
            return Expression.MakeTry(typeof(void), _try, _finally, null, catches);
        }

        public override Expression TryFault(Expression _try, Expression _fault) =>
            Expression.TryFault(_try, _fault);

        public override Expression TypeAs(Expression exp, Type castTo)
        {
            // In CIL, isinst on a value type yields a boxed object reference (or null),
            // allowing subsequent unbox/unbox.any or null checks.
            if (castTo.IsValueType)
            {
                var asObj = exp.Type == typeof(object) ? exp : (Expression)Expression.Convert(exp, typeof(object));
                if (exp is ParameterExpression || exp is ConstantExpression)
                    return Expression.Condition(Expression.TypeIs(exp, castTo), asObj, Expression.Constant(null, typeof(object)));
                var tmp = Expression.Variable(typeof(object), "_is");
                return Expression.Block(
                    new[] { tmp },
                    Expression.Assign(tmp, asObj),
                    Expression.Condition(Expression.TypeIs(tmp, castTo), tmp, Expression.Constant(null, typeof(object))));
            }
            return Expression.TypeAs(exp, castTo);
        }

        public override Expression Throw(Expression exp) =>
            Expression.Throw(exp);

        public override Expression Unbox(Expression value, Type type) =>
            Expression.Unbox(value, type);

        public override Expression Xor(Expression left, Expression right) =>
            Expression.ExclusiveOr(left, right);

        public override Expression Duplicate(Expression exp) =>
            exp;

        public override Expression AddressOf(Expression binding) =>
            // an array element's address must denote a writable location
            binding.NodeType == ExpressionType.ArrayIndex && binding is BinaryExpression b
                ? Expression.ArrayAccess(b.Left, b.Right)
                : binding;

        public override Expression ObjectInit(Expression address, Type objType) =>
            Expression.Assign(address, Expression.Default(objType));

        public override Expression EndFilter(Expression value) =>
            value;

        public override Expression Unaligned(Expression value) =>
            value;

        public static Expression<T> Decompile<T>(T func)
            where T : Delegate
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var method = func.Method;
            var dec = new LinqDecompiler(method.ReturnType);
            var body = method.Decompile(dec, out var args);

            // Unwrap a single tail return so simple lambdas stay clean (e.g. x => x + 1).
            if (dec.returnCount == 1 && TryUnwrapTailReturn(body, dec.returnTarget, out var unwrapped))
            {
                body = unwrapped;
            }
            else if (dec.returnCount > 0)
            {
                var def = method.ReturnType == typeof(void) ? (Expression)Expression.Empty() : Expression.Default(method.ReturnType);
                var label = Expression.Label(dec.returnTarget, def);
                body = body is BlockExpression blk && blk.Variables.Count == 0
                    ? Expression.Block(method.ReturnType, blk.Expressions.Concat(new[] { label }))
                    : Expression.Block(method.ReturnType, body, label);
            }

            body = method.ReturnType == typeof(void) ? body : Fit(body, method.ReturnType);

            var invokeParams = typeof(T).GetMethod("Invoke").GetParameters();
            var isOpenInstance = !method.IsStatic && func.Target == null && invokeParams.Length == args.Length;
            var parameters = (method.IsStatic || isOpenInstance ? args : args.Skip(1)).Cast<ParameterExpression>().ToList();

            // Declare the method's locals and the decompiler's temporaries, and bind 'this'
            // to the delegate's target for closed instance delegates.
            var free = FreeVariables.Find(body).Except(parameters).ToList();
            if (free.Count > 0)
            {
                var init = new List<Expression>();
                if (!method.IsStatic && !isOpenInstance && free.Contains(args[0]))
                    init.Add(Expression.Assign(args[0], Expression.Constant(func.Target, args[0].Type)));
                var exprs = body is BlockExpression b && b.Variables.Count == 0 ? b.Expressions : (IEnumerable<Expression>)new[] { body };
                var blockType = method.ReturnType == typeof(void) ? typeof(void) : body.Type;
                body = Expression.Block(blockType, free, init.Concat(exprs));
            }
            return Expression.Lambda<T>(body, parameters);
        }

        static bool TryUnwrapTailReturn(Expression body, LabelTarget target, out Expression unwrapped)
        {
            if (body is GotoExpression g && g.Kind == GotoExpressionKind.Return && g.Target == target)
            {
                unwrapped = g.Value ?? Expression.Empty();
                return true;
            }
            if (body is BlockExpression b && b.Variables.Count == 0 && b.Expressions.Count > 0)
            {
                var last = b.Expressions[b.Expressions.Count - 1];
                if (last is GotoExpression g2 && g2.Kind == GotoExpressionKind.Return && g2.Target == target)
                {
                    var exprs = new Expression[b.Expressions.Count];
                    for (int i = 0; i < exprs.Length - 1; ++i)
                        exprs[i] = b.Expressions[i];
                    exprs[exprs.Length - 1] = g2.Value ?? Expression.Empty();
                    unwrapped = Expression.Block(exprs);
                    return true;
                }
            }
            unwrapped = null;
            return false;
        }

        /// <summary>
        /// Finds the variables an expression uses without declaring them.
        /// </summary>
        sealed class FreeVariables : ExpressionVisitor
        {
            readonly List<ParameterExpression> used = new List<ParameterExpression>();
            readonly HashSet<ParameterExpression> declared = new HashSet<ParameterExpression>();

            public static IEnumerable<ParameterExpression> Find(Expression e)
            {
                var v = new FreeVariables();
                v.Visit(e);
                return v.used.Distinct().Where(p => !v.declared.Contains(p));
            }
            protected override Expression VisitParameter(ParameterExpression node)
            {
                used.Add(node);
                return node;
            }
            protected override Expression VisitBlock(BlockExpression node)
            {
                declared.UnionWith(node.Variables);
                return base.VisitBlock(node);
            }
            protected override CatchBlock VisitCatchBlock(CatchBlock node)
            {
                if (node.Variable != null)
                    declared.Add(node.Variable);
                return base.VisitCatchBlock(node);
            }
            protected override Expression VisitLambda<TLambda>(Expression<TLambda> node)
            {
                declared.UnionWith(node.Parameters);
                return base.VisitLambda(node);
            }
        }
    }
}
