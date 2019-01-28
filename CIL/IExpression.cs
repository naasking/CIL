using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIL
{
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
        //T Bool(T e);
        T Constant<TValue>(TValue value); // or should elaborate?
        T Constant(object value, Type type); // or should elaborate?
        T And(T left, T right);
        T Or(T left, T right);
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
        // 'obj' can be null for static methods
        T Call(MethodBase method, bool tailcall, T obj, IEnumerable<T> args);
        T CallIndirect(byte[] signature, T methodPtr, bool tailcall, IEnumerable<T> args);
        T GetPointer(MethodBase method, T obj);
        T Box(T exp);
        T Cast(T exp, Type castTo);
        T CastOverflow(T exp, Type castTo);
        T TypeAs(T exp, Type castTo);
        T Field(FieldInfo field, T obj);
        T Return(T exp);
        T Switch<TCase>(T value, IEnumerable<KeyValuePair<TCase, T>> branches);
        T Goto(T exp);
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
        T EndFilter(T value);
        T SizeOf(Type valueType);
        T TryCatchFinally(T _try, IEnumerable<KeyValuePair<Type, T>> _catch, T _finally);

        // control attributes
        //void Tailcall();
        //void Constrain(Type type);

        // extract info from expressions
        Type TypeOf(T exp);
        bool IsConstant(T exp);
        object Value(T exp);
    }
}
