using System;
using System.Reflection;
using System.Collections.Generic;

namespace CIL
{
    /// <summary>
    /// A catch handler, optionally guarded by an exception filter.
    /// </summary>
    /// <typeparam name="T">The type of expression being constructed.</typeparam>
    public struct CatchHandler<T>
    {
        /// <summary>The type of exception caught (System.Object for catch-all and filter handlers).</summary>
        public readonly Type ExceptionType;
        /// <summary>The variable bound to the exception object in the filter and body.</summary>
        public readonly T Exception;
        /// <summary>The handler body.</summary>
        public readonly T Body;
        /// <summary>The boolean filter expression, if <see cref="HasFilter"/>.</summary>
        public readonly T Filter;
        /// <summary>Whether the handler has a filter.</summary>
        public readonly bool HasFilter;

        /// <summary>Construct an unfiltered catch handler.</summary>
        public CatchHandler(Type exceptionType, T exception, T body)
            : this(exceptionType, exception, body, default(T), false)
        {
        }

        /// <summary>Construct a filtered catch handler.</summary>
        public CatchHandler(Type exceptionType, T exception, T body, T filter)
            : this(exceptionType, exception, body, filter, true)
        {
        }

        CatchHandler(Type exceptionType, T exception, T body, T filter, bool hasFilter)
        {
            ExceptionType = exceptionType;
            Exception = exception;
            Body = body;
            Filter = filter;
            HasFilter = hasFilter;
        }
    }

    /// <summary>
    /// An interface that abstracts over the expression form used to traverse CIL.
    /// </summary>
    /// <typeparam name="T">The type of expression being constructed.</typeparam>
    public interface IExpression<T>
    {
        //FIXME: this should perhaps be called ITranspiler or ICompiler

        T Param(ParameterInfo x);
        T Param(string name, Type type);
        T Local(LocalVariableInfo x);
        T Constant<TValue>(TValue value);
        T Constant(object value, Type type);
        T And(T left, T right);
        T Or(T left, T right);
        T AndAlso(T left, T right);
        T OrElse(T left, T right);
        T Xor(T left, T right);
        T Not(T exp);
        T LeftShift(T value, T shift);
        T RightShift(T value, T shift);
        T Add(T left, T right);
        T AddOverflow(T left, T right);
        T Subtract(T left, T right);
        T SubtractOverflow(T left, T right);
        T Multiply(T left, T right);
        T MultiplyOverflow(T left, T right);
        T Divide(T left, T right);
        T Modulo(T left, T right);
        T If(T cond, T _then, T _else);
        T If(T cond, T _then);
        T Label(string name);
        T MarkLabel(T label);
        T Goto(T label);
        T Break(T label);
        T Continue(T label);
        T Loop(T body, T breakLabel, T continueLabel);
        // 'obj' can be null for static methods
        T Property(T obj, PropertyInfo getter);
        T Property(T obj, PropertyInfo getter, IEnumerable<T> indexers);
        T Call(MethodBase method, bool tailcall, T obj, IEnumerable<T> args);
        T CallIndirect(byte[] signature, T methodPtr, bool tailcall, IEnumerable<T> args);
        T GetPointer(MethodBase method, T obj);
        T Box(T exp, Type type);
        T Cast(T exp, Type castTo);
        T CastOverflow(T exp, Type castTo);
        T TypeAs(T exp, Type castTo);
        T Field(FieldInfo field, T obj);
        T Return();
        T Return(T exp);
        /// <summary>
        /// A multiway branch: evaluate <paramref name="value"/> once, then the branch with a
        /// matching key, or <paramref name="defaultBranch"/> if none match.
        /// </summary>
        /// <remarks>Several keys may map to the same branch expression instance.</remarks>
        T Switch<TCase>(T value, IEnumerable<KeyValuePair<TCase, T>> branches, T defaultBranch);
        T Negate(T exp);
        T Equal(T lhs, T rhs);
        T NotEqual(T lhs, T rhs);
        T GreaterThan(T lhs, T rhs);
        T GreaterThanOrEqual(T lhs, T rhs);
        T LessThan(T lhs, T rhs);
        T LessThanOrEqual(T lhs, T rhs);
        T Throw(T exp);
        T Rethrow();
        T Duplicate(T exp);
        T New(ConstructorInfo ctor, IEnumerable<T> args);
        T Array(Type arrayType, IEnumerable<T> arrayBounds);
        T ArrayLength(T exp);
        T ArrayGet(T array, T index);
        T ArraySet(T array, T index, T value);
        T Assign(T variable, T value);
        T Unaligned(T value);
        T Unbox(T value, Type type);
        T Jump(MethodBase target);
        T AddressGet(T address);
        T AddressSet(T address, T value);
        T AddressOf(T binding);
        T LocalAlloc(T bytes);
        T ArgumentList();
        T ObjectInit(T address, Type objType);
        T BlockInit(T address, T value, T count);
        T BlockCopy(T dst, T src, T byteCount);
        T Block(IEnumerable<T> e);
        T StructCopy(T dst, T src, Type structType);
        T MakeRefAny(T address, Type type);
        T RefAnyValue(T typedRef, Type type);
        T RefAnyType(T typedRef);
        T EndFilter(T value);
        T SizeOf(Type valueType);
        /// <summary>
        /// A protected block used as a statement. Each handler binds the caught exception to
        /// <see cref="CatchHandler{T}.Exception"/>; <paramref name="_finally"/> is default(T)
        /// when there is no finally block.
        /// </summary>
        T TryCatchFinally(T _try, IEnumerable<CatchHandler<T>> handlers, T _finally);
        /// <summary>
        /// A protected block with a fault handler that runs only when an exception is thrown.
        /// </summary>
        T TryFault(T _try, T _fault);
    }
}
