using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using CIL;

namespace Genops
{
    class Program
    {
        public delegate TValue Getter<TStruct, TValue>(ref TStruct x);
        static void Main(string[] args)
        {
            var type = typeof(OpCodes);
            var optype = typeof(OpCode);
            var getValue = Delegate.CreateDelegate(typeof(Getter<OpCode, short>), null, optype.GetMethod("get_Value", BindingFlags.Public | BindingFlags.Instance)) as Getter<OpCode, short>;
            var getName = Delegate.CreateDelegate(typeof(Getter<OpCode, string>), null, optype.GetMethod("get_Name", BindingFlags.Public | BindingFlags.Instance)) as Getter<OpCode, string>;
            //var getSize = Delegate.CreateDelegate(typeof(Getter<OpCode, int>), null, optype.GetMethod("get_Size", BindingFlags.Public | BindingFlags.Instance)) as Getter<OpCode, int>;
            //var isSingleByte = Delegate.CreateDelegate(typeof(Getter<OpCode, bool>), null, optype.GetMethod("TakesSingleByteArgument", BindingFlags.Public | BindingFlags.Instance)) as Getter<OpCode, bool>;
            var sb = new StringBuilder();
            sb.AppendLine("using System;")
              .AppendLine("using System.Reflection.Emit;")
              .AppendLine()
              .AppendLine("namespace CIL")
              .AppendLine("{")
              .AppendLine("    public enum OpType : short")
              .AppendLine("    {");
            var opmap = new Dictionary<short, string>();
            foreach (var x in type.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                var op = (OpCode)x.GetValue(null);
                var name = getName(ref op);
                var value = getValue(ref op);
                name = (char.ToUpper(name[0]) + name.Substring(1)).Replace('.', '_');
                opmap[value] = name;
                sb.AppendFormat("        {0} = {1}," + Environment.NewLine, name, value);
            }
            sb.AppendLine("    }");
            //  .AppendLine("    public static partial class IL")
            //  .AppendLine("    {")
            //  .AppendLine("        public static OpCode GetOpCodeAt(byte[] ops, int index)")
            //  .AppendLine("        {")
            //  .AppendLine("            switch(op[index])")
            //  .AppendLine("            {");
            //foreach (var x in type.GetFields(BindingFlags.Static | BindingFlags.Public))
            //{
            //    var op = (OpCode)x.GetValue(null);
            //    if (isSingleByte(ref op))
            //    var value = getValue(ref op);
            //    sb.AppendFormat("                case {0}: return OpCodes.{1};" + Environment.NewLine, value, x.Name);
            //}
            //sb.AppendLine("                default: throw new ArgumentException(\"Unknown op code type: \" + op);")
            //  .AppendLine("            }")
            //  .AppendLine("        }")
            //  .AppendLine("    }")
            sb  .AppendLine("}");

            File.WriteAllText("../../../CIL/OpType.cs", sb.ToString());

            //var il = CIL.IL.Read(getName.Method.Module, getName.Method.GetMethodBody().GetILAsByteArray());
            var method = new Action<string[]>(Main).Method;
            var il = method.GetILReader();
        }
    }
}
