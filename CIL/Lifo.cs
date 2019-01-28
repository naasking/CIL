using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CIL
{
    /// <summary>
    /// A purely functional stack.
    /// </summary>
    /// <remarks>
    /// "null" is also a valid sequence value that can be used to
    /// construct lists (see example).
    /// </remarks>
    /// <example>
    /// <code>Seq&lt;T&gt; list = value1 &amp; value2 &amp; null;</code>
    /// </example>
    /// <typeparam name="T">The type of the sequence elements.</typeparam>
    sealed class Lifo<T> : IEnumerable<T>
    {
        /// <summary>
        /// Construct a new sequence from a new head value and an existing list.
        /// </summary>
        /// <param name="value">The new value at the head of the list.</param>
        /// <param name="next">The remainder of the list.</param>
        public Lifo(T value, Lifo<T> next)
        {
            this.Next = next;
            this.Value = value;
        }

        /// <summary>
        /// Construct a new single-element sequence.
        /// </summary>
        /// <param name="value">The new value at the head of the list.</param>
        public Lifo(T value) : this(value, Empty)
        {
        }

        /// <summary>
        /// Returns an empty stack.
        /// </summary>
        public static Lifo<T> Empty
        {
            get { return default(Lifo<T>); }
        }

        /// <summary>
        /// Count the items in the list.
        /// </summary>
        public int Count { get => 1 + (next?.Count ?? 0); }

        /// <summary>
        /// Returns an enumerator over the given list.
        /// </summary>
        /// <returns>An enumeration over the list.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (var t = this; t != null; t = t.Next)
            {
                yield return t.Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((Lifo<T>)this).GetEnumerator();
        }

        /// <summary>
        /// Returns the first item.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the collection is empty.</exception>
        public T Value { get; private set; }

        Lifo<T> next;

        /// <summary>
        /// Returns the next element in the sequence.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the collection is empty.</exception>
        public Lifo<T> Next
        {
            get { return next; }
            private set { next = value; }
        }

        ///// <summary>
        ///// Destructively concatenate two Lifo. Only to be used internally when
        ///// the semantics are still outwardly immutable.
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <param name="x"></param>
        ///// <param name="other"></param>
        //internal bool AppendDestructive(Lifo<T> other)
        //{
        //    var x = this;
        //    while (x.Next != null)
        //        x = x.Next;
        //    return null == System.Threading.Interlocked.CompareExchange(ref x.next, other, null);
        //}

        /// <summary>
        /// Tests structural equality of two sequences.
        /// </summary>
        /// <param name="other">The other sequence to compare to.</param>
        /// <returns>True if the sequences are equal, false otherwise.</returns>
        public bool Equals(Lifo<T> other)
        {
            return ReferenceEquals(this, other)
                || !ReferenceEquals(other, null) && this.SequenceEqual(other);
        }

        /// <summary>
        /// Compares two objects for equality.
        /// </summary>
        /// <param name="obj">The other object to compare to.</param>
        /// <returns>Returns true if the objects are equal.</returns>
        public override bool Equals(object obj)
        {
            return obj is Lifo<T> && Equals((Lifo<T>)obj);
        }

        /// <summary>
        /// Returns the hash code for the current sequence.
        /// </summary>
        /// <returns>The integer hash code.</returns>
        public override int GetHashCode()
        {
            return EqualityComparer<T>.Default.GetHashCode(Value)
                 ^ typeof(Lifo<T>).GetHashCode();
        }

        /// <summary>
        /// Pops the first element off the sequence.
        /// </summary>
        /// <param name="value">The value in the first element of the sequence.</param>
        /// <returns>The remaining sequence.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the sequence is empty.</exception>
        public Lifo<T> Pop(out T value)
        {
            value = this.Value;
            return Next;
        }

        /// <summary>
        /// Return a string representation of the given list.
        /// </summary>
        /// <returns>String represetation of the list.</returns>
        public override string ToString()
        {
            return ToString(new StringBuilder("[")).Append(']').ToString();
        }

        StringBuilder ToString(StringBuilder buffer)
        {
            return (ReferenceEquals(Next, null) ? buffer.Append("()") : Next.ToString(buffer)).Append(" & ").Append(Value);
        }

        /// <summary>
        /// The sequence 'cons'/add operation, to construct a sequence from a new value and an existing list.
        /// </summary>
        /// <param name="right">The new value at the head of the list.</param>
        /// <param name="left">The remainder of the list.</param>
        /// <returns>A new sequence constructed from the given parameters.</returns>
        public static Lifo<T> operator &(Lifo<T> left, T right)
        {
            return left.Push(right);
        }

        /// <summary>
        /// The sequence 'cons'/add operation, to construct a sequence from two lists.
        /// </summary>
        /// <param name="left">The new value at the head of the list.</param>
        /// <param name="right">The remainder of the list.</param>
        /// <returns>A new sequence constructed from the given parameters.</returns>
        public static Lifo<T> operator &(Lifo<T> left, Lifo<T> right)
        {
            return left.Append(right);
        }

        /// <summary>
        /// Returns the value at the head of the sequence o, if o is not empty, or t otherwise. This is
        /// the sequence equivalent of the ?? operator for null values.
        /// </summary>
        /// <param name="left">The sequence value to return if not empty.</param>
        /// <param name="right">The value to return otherwise.</param>
        /// <returns>Either the head of the list, or t.</returns>
        public static T operator |(Lifo<T> left, T right)
        {
            return left.IsEmpty() ? right : left.Value;
        }

        /// <summary>
        /// Test two sequences for equality.
        /// </summary>
        /// <param name="left">The left sequence.</param>
        /// <param name="right">The right sequence.</param>
        /// <returns>Returns true if they are equal.</returns>
        public static bool operator ==(Lifo<T> left, Lifo<T> right)
        {
            return ReferenceEquals(left, right) || left.Equals(right);
        }

        /// <summary>
        /// Test two sequences for inequality.
        /// </summary>
        /// <param name="left">The left sequence.</param>
        /// <param name="right">The right sequence.</param>
        /// <returns>Returns true if they are not equal.</returns>
        public static bool operator !=(Lifo<T> left, Lifo<T> right)
        {
            return !ReferenceEquals(left, right) && !left.Equals(right);
        }
    }

    /// <summary>
    /// Extension methods on collection types.
    /// </summary>
    static class Extensions
    {
        /// <summary>
        /// True if the sequence is empty.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="x"></param>
        /// <returns></returns>
        public static bool IsEmpty<T0>(this Lifo<T0> x)
        {
            return x == null;
        }

        internal static StringBuilder Append<T>(this StringBuilder output, IEnumerable<T> source, string separator)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (separator == null) throw new ArgumentNullException("separator");
            if (output == null) throw new ArgumentNullException("output");
            foreach (var t in source)
            {
                var value = t is ValueType || t != null ? t.ToString() : string.Empty;
                output.Append(value);
                output.Append(separator);
            }
            return output;
        }

        #region Lifo semantics
        public static Lifo<T> Pop2<T>(this Lifo<T> lifo, out T first, out T second) =>
            lifo.Pop(out first).Pop(out second);

        /// <summary>
        /// Push an element on to the front of the sequence.
        /// </summary>
        /// <param name="value">The new head of the sequence.</param>
        /// <returns>A new sequence.</returns>
        public static Lifo<T> Push<T>(this Lifo<T> x, T value)
        {
            return new Lifo<T>(value, x);
        }

        /// <summary>
        /// Append the given sequence after the current sequence.
        /// </summary>
        /// <param name="other">The elements to append.</param>
        /// <returns>A new sequence constructed from the given parameters.</returns>
        public static Lifo<T> Append<T>(this Lifo<T> first, Lifo<T> other)
        {
            return first.IsEmpty() ? other : first.Next.Append(other).Push(first.Value);
        }

        /// <summary>
        /// Remove an element from the sequence.
        /// </summary>
        /// <param name="value">The value to remove.</param>
        /// <returns>A new sequence without the element.</returns>
        public static Lifo<T> Remove<T>(this Lifo<T> x, T value)
        {
            if (x.IsEmpty())
                return x;
            if (EqualityComparer<T>.Default.Equals(x.Value, value))
                return x.Next;
            var y = x.Next.Remove(value);
            return ReferenceEquals(y, x.Next) ? y : y.Push(x.Value);
        }

        /// <summary>
        /// Checks whether a value is in the sequence.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns>True if the element is in the sequence, false otherwise.</returns>
        public static bool Contains<T>(this Lifo<T> x, T value)
        {
            var eq = EqualityComparer<T>.Default;
            for (var s = x; !s.IsEmpty(); s = s.Next)
            {
                if (eq.Equals(value, s.Value))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Reverse a sequence.
        /// </summary>
        /// <returns>A reversed sequence.</returns>
        public static Lifo<T> Reverse<T>(this Lifo<T> x)
        {
            var rev = Lifo<T>.Empty;
            for (var s = x; !s.IsEmpty(); s = s.Next)
            {
                rev = rev.Push(s.Value);
            }
            return rev;
        }

        /// <summary>
        /// Reverses the current sequence and appends another sequence to the end.
        /// </summary>
        /// <param name="append">The sequence to append.</param>
        /// <returns>A combined sequence.</returns>
        public static Lifo<T> ReverseAppend<T>(this Lifo<T> x, Lifo<T> append)
        {
            for (var s = x; !s.IsEmpty(); s = s.Next)
            {
                append = append.Push(s.Value);
            }
            return append;
        }

        /// <summary>
        /// Reverses the current sequence and appends it after other sequence.
        /// </summary>
        /// <param name="other">The sequence to append.</param>
        /// <returns>A combined sequence.</returns>
        public static Lifo<T> ReversePush<T>(this Lifo<T> x, Lifo<T> other)
        {
            return other.IsEmpty() ? x.Reverse() : x.ReversePush(other.Next).Push(other.Value);
        }
        #endregion
    }
}
