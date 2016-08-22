# CIL

An extremely lightweight CIL bytecode processing library:

    using CIL;

    class Program
    {
        static void Main(string[] args)
        {
			var main = new Action<string[]>(Main).Method;	// obtain MethodInfo for Program.Main
			var il = main.GetInstructions();				// return IEnumerable<Instruction>
			foreach (var x in il)
			{
				switch(x.OpCode.Type())
				{
					case OpType.Add:
						Console.WriteLine("Add {0}", x.Operand.Int32);
						break;
					case OpType.Sub:
						...
					case OpType.Call:
						Console.WriteLine("Call {0}", x.ResolveMethod());
						break;
					...
				}
			}
		}
	}

Most other solutions like Mono.Cecil or JSIL are either very heavy
weight, or IMO too monolithic by comparison. Right now CIL focuses purely
on reading CIL instructions using the native System.Reflection and
System.Reflection.Emit types.

# CIL.Expressions

This subproject decompiles CIL into a corresponding System.Linq.Expressions.Expression.
Most instructions are supported given the expanded expression types available in .NET
4.0, although branching and switch instructions are still in progress (but doable):

    Func<int, bool> foo = x => 3 != x;
    var decompiled = foo.GetExpression();
	Console.WriteLine(decompiled);		  // prints: x => 3 != x

CIL loses some type information that is available at a higher level, so I'm still
working out a few kinks. For instance, System.Boolean and System.Char don't exist
in CIL, nor do enums, they're simply handled as a "native int", so the fact CIL is
operating on a bool or char has to be reverse engineered from context.

# Status

CIL reading/traversal works, now also including a nice interface for analyzing switch
instructions.

CIL.Expressions should currently decompile any CIL that doesn't use:

 * unverifiable or odd instructions: jmp, arglist, prefix*
 * address instructions, ie. ldarga, ldelema, ldflda, ldloca, ldobj, localloc, refanyval, cpblk, cpobj, initblk, initobj, etc.
 * exception handling instructions: temporary limitation until I fill this out.

Please do let me know if you find some mistaken in the translation or some
undocumented limitation.

# Future Work

I plan to implement a few more CIL translations:

 * CIL.Sql: compilation to SQL queries
 * CIL.JavaScript: compilation to JavaScript

LINQ expressions were a neat idea, but the implementation is very heavyweight and
not amenable to caching. For instance, LINQ queries for SQL backends create a
deep expression tree (N allocations for N nodes), only to immediately throw it away
after generating SQL. The sharp distinction between code and expressions means
you can't reuse code as an expression, or an expression as code, thus inhibiting
code reuse. For instance, you can't use a property computed from two other
properties in an SQL query, ie. FullName = FirstName + ' ' + LastName.

By constrast, a query built from the CIL needs only a single allocation of the
precompiled closure, and compilation itself merely allocates a byte array for
the CIL instructions. Furthermore, computed properties can be fully supported,
thus enable code reuse between code and expressions.

Whether this will turn out to be workable in practice remains to be seen.
