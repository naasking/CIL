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
    public interface IDecompiler<T>
    {
        T Param(ParameterInfo x);
        T Local(LocalVariableInfo x);
        //T Bool(T e);
        T Constant<TValue>(TValue value); // or should elaborate?
        T And(T left, T right);
        T Add(T left, T right);
        T AddOverflow(T left, T right);
        T If(T cond, T _then, T _else);
        // 'obj' can be null for static methods
        T Call(MethodBase method, T obj, IEnumerable<T> args);
        // 'obj' cannot be null
        T CallVirt(MethodBase method, T obj, IEnumerable<T> args);
        T Box(T exp);
        T Cast(T exp, Type castTo);
        T TypeAs(T exp, Type castTo);
        T Field(FieldInfo field, T obj);
        T ArrayLength(T exp);
        T Return(T exp);
        T Switch(T value, IEnumerable<T> branches);

        Type Typeof(T exp);
        bool IsConstant(T exp);
        object ValueOf(T exp);
    }
}
