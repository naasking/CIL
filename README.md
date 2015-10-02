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
					case OpType.Add: Console.WriteLine("Add {0}", x.Operand);
					case OpType.Sub: ...
					case OpType.Call: Console.WriteLine("Call {0}", main.Module.ResolveMethod(x.Operand.MetadataToken));
					...
				}
			}
		}
	}

Most other solutions like Mono.Cecil or JSIL, are either very heavy
weight or too monolithic by comparison. Right now this focuses purely
on reading CIL instructions using the native System.Reflection and
System.Reflection.Emit types.
