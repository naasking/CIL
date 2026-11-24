using System;
using System.Reflection;
using System.Collections.Generic;

namespace CIL
{
    public class ExpressionError<T> : IExpression<T>
    {
        public virtual T Param(ParameterInfo x) { throw new NotSupportedException(); }
        public virtual T Param(string name, Type type) { throw new NotSupportedException(); }
        public virtual T Local(LocalVariableInfo x) { throw new NotSupportedException(); }
        public virtual T Constant<TValue>(TValue value) { throw new NotSupportedException(); }
        public virtual T Constant(object value, Type type) { throw new NotSupportedException(); }
        public virtual T And(T left, T right) { throw new NotSupportedException(); }
        public virtual T Or(T left, T right) { throw new NotSupportedException(); }
        public virtual T AndAlso(T left, T right) { throw new NotSupportedException(); }
        public virtual T OrElse(T left, T right) { throw new NotSupportedException(); }
        public virtual T Xor(T left, T right) { throw new NotSupportedException(); }
        public virtual T Not(T exp) { throw new NotSupportedException(); }
        public virtual T LeftShift(T value, T shift) { throw new NotSupportedException(); }
        public virtual T RightShift(T value, T shift) { throw new NotSupportedException(); }
        public virtual T Add(T left, T right) { throw new NotSupportedException(); }
        public virtual T AddOverflow(T left, T right) { throw new NotSupportedException(); }
        public virtual T Subtract(T left, T right) { throw new NotSupportedException(); }
        public virtual T SubtractOverflow(T left, T right) { throw new NotSupportedException(); }
        public virtual T Multiply(T left, T right) { throw new NotSupportedException(); }
        public virtual T MultiplyOverflow(T left, T right) { throw new NotSupportedException(); }
        public virtual T Divide(T left, T right) { throw new NotSupportedException(); }
        public virtual T Modulo(T left, T right) { throw new NotSupportedException(); }
        public virtual T If(T cond, T _then, T _else) { throw new NotSupportedException(); }
        public virtual T If(T cond, T _then) { throw new NotSupportedException(); }
        public virtual T Label(string name) { throw new NotSupportedException(); }
        public virtual T MarkLabel(T label) { throw new NotSupportedException(); }
        public virtual T Goto(T label) { throw new NotSupportedException(); }
        public virtual T Break(T label) { throw new NotSupportedException(); }
        public virtual T Continue(T label) { throw new NotSupportedException(); }
        public virtual T Loop(T body, T breakLabel, T continueLabel) { throw new NotSupportedException(); }
        // 'obj' can be null for static methods
        public virtual T Property(T obj, PropertyInfo getter) { throw new NotSupportedException(); }
        public virtual T Property(T obj, PropertyInfo getter, IEnumerable<T> indexers) { throw new NotSupportedException(); }
        public virtual T Call(MethodBase method, bool tailcall, T obj, IEnumerable<T> args) { throw new NotSupportedException(); }
        public virtual T CallIndirect(byte[] signature, T methodPtr, bool tailcall, IEnumerable<T> args) { throw new NotSupportedException(); }
        public virtual T GetPointer(MethodBase method, T obj) { throw new NotSupportedException(); }
        public virtual T Box(T exp, Type type) { throw new NotSupportedException(); }
        public virtual T Cast(T exp, Type castTo) { throw new NotSupportedException(); }
        public virtual T CastOverflow(T exp, Type castTo) { throw new NotSupportedException(); }
        public virtual T TypeAs(T exp, Type castTo) { throw new NotSupportedException(); }
        public virtual T Field(FieldInfo field, T obj) { throw new NotSupportedException(); }
        public virtual T Return() { throw new NotSupportedException(); }
        public virtual T Return(T exp) { throw new NotSupportedException(); }
        public virtual T Switch<TCase>(T value, IEnumerable<KeyValuePair<TCase, T>> branches, T defaultBranch) { throw new NotSupportedException(); }
        public virtual T Negate(T exp) { throw new NotSupportedException(); }
        public virtual T Equal(T lhs, T rhs) { throw new NotSupportedException(); }
        public virtual T NotEqual(T lhs, T rhs) { throw new NotSupportedException(); }
        public virtual T GreaterThan(T lhs, T rhs) { throw new NotSupportedException(); }
        public virtual T GreaterThanOrEqual(T lhs, T rhs) { throw new NotSupportedException(); }
        public virtual T LessThan(T lhs, T rhs) { throw new NotSupportedException(); }
        public virtual T LessThanOrEqual(T lhs, T rhs) { throw new NotSupportedException(); }
        public virtual T Throw(T exp) { throw new NotSupportedException(); }
        public virtual T Rethrow() { throw new NotSupportedException(); }
        public virtual T Duplicate(T exp) { throw new NotSupportedException(); }
        public virtual T New(ConstructorInfo ctor, IEnumerable<T> args) { throw new NotSupportedException(); }
        public virtual T Array(Type arrayType, IEnumerable<T> arrayBounds) { throw new NotSupportedException(); }
        public virtual T ArrayLength(T exp) { throw new NotSupportedException(); }
        public virtual T ArrayGet(T array, T index) { throw new NotSupportedException(); }
        public virtual T ArraySet(T array, T index, T value) { throw new NotSupportedException(); }
        public virtual T Assign(T variable, T value) { throw new NotSupportedException(); }
        public virtual T Unaligned(T value) { throw new NotSupportedException(); }
        public virtual T Unbox(T value, Type type) { throw new NotSupportedException(); }
        public virtual T Jump(MethodBase target) { throw new NotSupportedException(); }
        public virtual T AddressGet(T address) { throw new NotSupportedException(); }
        public virtual T AddressSet(T address, T value) { throw new NotSupportedException(); }
        public virtual T AddressOf(T binding) { throw new NotSupportedException(); }
        public virtual T LocalAlloc(T bytes) { throw new NotSupportedException(); }
        public virtual T ArgumentList() { throw new NotSupportedException(); }
        public virtual T ObjectInit(T address, Type objType) { throw new NotSupportedException(); }
        public virtual T BlockInit(T address, T value, T count) { throw new NotSupportedException(); }
        public virtual T BlockCopy(T dst, T src, T byteCount) { throw new NotSupportedException(); }
        public virtual T Block(IEnumerable<T> exp) { throw new NotSupportedException(); }
        public virtual T StructCopy(T dst, T src, Type structType) { throw new NotSupportedException(); }
        public virtual T MakeRefAny(T address, Type type) { throw new NotSupportedException(); }
        public virtual T RefAnyValue(T typedRef, Type type) { throw new NotSupportedException(); }
        public virtual T RefAnyType(T typedRef) { throw new NotSupportedException(); }
        public virtual T EndFilter(T value) { throw new NotSupportedException(); }
        public virtual T SizeOf(Type valueType) { throw new NotSupportedException(); }
        public virtual T TryCatchFinally(T _try, IEnumerable<CatchHandler<T>> handlers, T _finally) { throw new NotSupportedException(); }
        public virtual T TryFault(T _try, T _fault) { throw new NotSupportedException(); }
    }
}
