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
        int count;
        int[] labelIndex;
        ExceptionRegion[] handlers;

        internal Code(int byteCodeLength)
        {
            count = 0;
            instructions = new Instruction[byteCodeLength];
            labelIndex = new int[byteCodeLength];
            handlers = Array.Empty<ExceptionRegion>();
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
        public IL.Label GetLabel(int index) => instructions[index].label;

        /// <summary>
        /// Gets the corresponding instruction index for the given label.
        /// </summary>
        /// <param name="index">The label to map to an instruction index.</param>
        /// <returns>The instruction's index.</returns>
        public int GetIndex(IL.Label label) =>
            labelIndex[label.Offset];

        /// <summary>
        /// The exception handling regions for this method.
        /// </summary>
        public ExceptionRegion[] ExceptionHandlers => handlers;

        /// <summary>
        /// Sets the exception handling regions and marks boundary instructions.
        /// </summary>
        internal void SetExceptionHandlers(ExceptionRegion[] regions)
        {
            this.handlers = regions;
            for (int i = 0; i < regions.Length; i++)
            {
                ref var r = ref regions[i];
                this[r.TryStart].flags |= InstructionFlags.TryStart | InstructionFlags.Leader;
                this[r.HandlerStart].flags |= InstructionFlags.HandlerStart | InstructionFlags.Leader;
                if (r.IsFilter)
                    this[r.FilterStart].flags |= InstructionFlags.FilterStart | InstructionFlags.Leader;
                // Mark exclusive end boundaries as leaders if within the bytecode
                if (r.TryEnd.Offset < labelIndex.Length)
                    this[r.TryEnd].flags |= InstructionFlags.Leader;
                if (r.HandlerEnd.Offset < labelIndex.Length)
                    this[r.HandlerEnd].flags |= InstructionFlags.Leader;
            }
        }

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
                labelIndex[offset + i] = count;
            ++count;
            return size;
        }

        internal void SetFlag(int index, InstructionFlags flag) =>
            instructions[index].flags |= flag;

        internal void AddLoop(IL.Label jumpTarget) =>
            this[jumpTarget].flags |= InstructionFlags.LoopHeader;

        internal void AddBreak(IL.Label jumpTarget) =>
            this[jumpTarget].flags |= InstructionFlags.BreakTarget;

        /// <inheritdoc/>
        public override string ToString()
        {
            var buf = new StringBuilder();
            for (int i = 0; i < count; ++i)
                buf.Append(instructions[i].label).Append(": ").Append(instructions[i]).AppendLine();
            return buf.ToString();
        }
    }
}
