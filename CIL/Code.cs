using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace CIL
{
    /// <summary>
    /// Encapsulates an instruction stream.
    /// </summary>
    public struct Code : IEnumerable<Instruction>
    {
        // Instructions may take more than 1 byte, so number of decoded instructions
        // is always strictly lower than the length of the bytecode stream. The 
        // labelIndex enables mapping a label to an instruction index in constant time.
        Instruction[] instructions;
        ushort count;
        ushort[] labelIndex;

        internal Code(int byteCodeLength)
        {
            count = 0;
            instructions = new Instruction[byteCodeLength];
            labelIndex = new ushort[byteCodeLength];
        }

        /// <summary>
        /// The instruction for the given label.
        /// </summary>
        /// <param name="label">The instruction's offset in the bytecode stream.</param>
        /// <returns>A reference to the instruction.</returns>
        public ref Instruction this[IL.Label label] => ref this[labelIndex[label.Offset]];

        /// <summary>
        /// The instruction at the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>A reference to the instruction.</returns>
        public ref Instruction this[int index] => ref instructions[index];

        /// <summary>
        /// The number of instructions.
        /// </summary>
        public int Count => count;

        /// <summary>
        /// Gets the label for the corresponding instruction.
        /// </summary>
        /// <param name="index">The index of the instruction to find.</param>
        /// <returns>The instruction's label.</returns>
        public IL.Label GetLabel(int index) =>
            new IL.Label(Array.IndexOf(labelIndex, (ushort)index));

        /// <summary>
        /// Gets the corresponding instruction index for the given label.
        /// </summary>
        /// <param name="index">The label to map to an instruction index.</param>
        /// <returns>The instruction's index.</returns>
        public int GetIndex(IL.Label label) =>
            labelIndex[label.Offset];

        /// <inheritdoc/>
        public IEnumerator<Instruction> GetEnumerator()
        {
            for (var i = 0; i < count; ++i)
                yield return instructions[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Add a new instruction.
        /// </summary>
        /// <param name="offset">The current instruction being added.</param>
        /// <param name="instr">The instruction to add.</param>
        /// <returns>The size of the opcode.</returns>
        internal int Add(int offset, ref Instruction instr)
        {
            instructions[count] = instr;
            var size = instr.Size;
            for (var i = 0; i < size; ++i)
                labelIndex[offset + i] = (ushort)count;
            ++count;
            return size;
        }

        /// <summary>
        /// Add a loop
        /// </summary>
        /// <param name="offset">The offset/label of the branch instruction.</param>
        /// <param name="jumpTarget">The target of the branch instruction.</param>
        internal void AddLoop(int offset, IL.Label jumpTarget) =>
            this[jumpTarget].AddLoop(new IL.Label(offset));

        /// <inheritdoc/>
        public override string ToString()
        {
            var buf = new StringBuilder();
            for (int i = 0, label = 0; i < count; label += instructions[i].Size, ++i)
                buf.Append(new IL.Label(label)).Append(": ").Append(instructions[i]).AppendLine();
            return buf.ToString();
        }
    }
}
