using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CIL.Expressions;

namespace CIL.Tests
{
    /// <summary>
    /// Checks that a decompiled method computes the same results as the original method,
    /// rather than just checking the shape of the expression tree.
    /// </summary>
    static class Equivalence
    {
        /// <summary>
        /// Decompile <paramref name="f"/>, compile the result, and check that for every input it
        /// returns an equal value, or throws the same type of exception, as <paramref name="f"/>.
        /// </summary>
        /// <param name="observe">Optional observable side effect (e.g. a static counter),
        /// compared after each call; <paramref name="reset"/> restores it before each call.</param>
        public static Expression<TDelegate> Check<TDelegate>(TDelegate f, Func<object> observe, Action reset, params object[][] inputs)
            where TDelegate : Delegate
        {
            var decompiled = LinqDecompiler.Decompile(f);
            var g = decompiled.Compile();
            foreach (var input in inputs.Length == 0 ? new[] { new object[0] } : inputs)
            {
                reset?.Invoke();
                var expected = Invoke(f, input);
                var expectedEffect = observe?.Invoke();
                reset?.Invoke();
                var actual = Invoke(g, input);
                var actualEffect = observe?.Invoke();
                Xunit.Assert.True(Same(expected, actual) && Equals(expectedEffect, actualEffect),
                    $"{f.Method.Name}({string.Join(", ", input.Select(Show))}): expected {Show(expected)}" +
                    (observe == null ? "" : $" [{Show(expectedEffect)}]") + $", got {Show(actual)}" +
                    (observe == null ? "" : $" [{Show(actualEffect)}]") + $"\n{decompiled}");
            }
            return decompiled;
        }

        /// <summary>
        /// Decompile <paramref name="f"/> and check it against the original on every input.
        /// </summary>
        public static Expression<TDelegate> Check<TDelegate>(TDelegate f, params object[][] inputs)
            where TDelegate : Delegate
        {
            return Check(f, null, null, inputs);
        }

        /// <summary>
        /// One input per value, for single-parameter methods.
        /// </summary>
        public static object[][] Each(params object[] values) =>
            values.Select(v => new[] { v }).ToArray();

        static object Invoke(Delegate d, object[] args)
        {
            // arrays are copied so that a call can't affect the inputs of the next one
            var copy = args.Select(a => a is Array arr ? arr.Clone() : a).ToArray();
            try
            {
                return d.DynamicInvoke(copy);
            }
            catch (TargetInvocationException e)
            {
                return e.InnerException;
            }
        }

        static bool Same(object a, object b)
        {
            if (a is Exception ea || b is Exception)
                return a?.GetType() == b?.GetType();
            if (a is IEnumerable xa && !(a is string) && b is IEnumerable xb)
                return xa.Cast<object>().SequenceEqual(xb.Cast<object>());
            return Equals(a, b);
        }

        static string Show(object o) =>
            o == null          ? "null" :
            o is string s      ? "\"" + s + "\"" :
            o is Exception e   ? e.GetType().Name :
            o is IEnumerable x ? "[" + string.Join(", ", x.Cast<object>().Select(Show)) + "]" :
                                 o.ToString();
    }
}
