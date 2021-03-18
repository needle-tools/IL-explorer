using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Disassembler.Editor.IL.OpCodesInfo;
using Disassembler.Editor.Plugins.SDILReader;
using UnityEngine;

namespace Disassembler.Editor.IL
{
    public class ILInstructionInfo
    {
        public readonly string Line;
        public readonly OpCodeInfo OpInfo; 

        public readonly int InstructionOffset;
        public bool IsBrTarget { get; }
        public readonly int BranchTarget = -1;
        public bool IsBlockStart => jumpsFromOtherLines.Count > 0;
        public readonly IReadOnlyList<int> LocalVariableAccesses = new List<int>();
        public object Operand => instruction.Operand;
        public OperandType OperandType => instruction.Code.OperandType;

        public bool IsConditionalBranch;
        public bool Is_BrIfTrue;

        private readonly ILInstruction instruction;
        private readonly HashSet<ILInstructionInfo> jumpsFromOtherLines = new HashSet<ILInstructionInfo>();
        private readonly HashSet<ILInstructionInfo> jumpsToOtherLines = new HashSet<ILInstructionInfo>();

        public IEnumerable<ILInstructionInfo> JumpsToOtherLineses => jumpsToOtherLines;
        public IEnumerable<ILInstructionInfo> JumpsFromOtherLines => jumpsFromOtherLines;

        public ILInstructionInfo(string line, ILInstruction instruction, OpCodeInfo opInfo)
        {
            this.Line = line;
            this.OpInfo = opInfo;
            this.instruction = instruction;
            this.InstructionOffset = instruction.Offset;
            this.IsBrTarget = instruction.IsBrTarget;
            
            if (this.IsBrTarget)
            {
                this.BranchTarget = (int) instruction.Operand;
            }
            else if (instruction.IsVariableAccess)
            {
                var list = new List<int>();
                var varIndex = Convert.ToInt32(instruction.Operand);
//                Debug.Log(instruction.Operand + " -> " + varIndex);
                if (!list.Contains(varIndex))
                    list.Add(varIndex);
                LocalVariableAccesses = list;
            }
            else if (instruction.Code.ToString().ToLowerInvariant().Contains("loc."))
            {
                // getting local variable access in a hacky way:
                var str = instruction.Code.ToString().ToLowerInvariant();
//                Debug.Log(instruction.Code);
                var indexOfDot = str.IndexOf('.');
                var sub = str.Substring(indexOfDot + 1);
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    var num = Convert.ToInt32(sub);
                    var list = new List<int>();
                    if (!list.Contains(num))
                        list.Add(num);
                    LocalVariableAccesses = list;
                }
            }
        }

        public void Resolve(IEnumerable<ILInstructionInfo> instructions)
        {
            foreach (var other in instructions)
            {
                if (other == this) continue;
                if (other.BranchTarget != this.InstructionOffset) continue;
                other.RegisterJumpToLine(this);
                this.RegisterJumpFromLine(other);
            }
        }

        private void RegisterJumpFromLine(ILInstructionInfo other)
        {
            if (other == this) return;
            if (jumpsFromOtherLines.Contains(other)) return;
            jumpsFromOtherLines.Add(other);
        }

        private void RegisterJumpToLine(ILInstructionInfo other)
        {
            if (other == this) return;
            if (jumpsToOtherLines.Contains(other)) return;
            jumpsToOtherLines.Add(other);

            var instructionCode = this.instruction.Code.ToString().ToLowerInvariant();
            this.IsConditionalBranch = instructionCode.Contains("true") || instructionCode.Contains("false");
            this.Is_BrIfTrue = IsConditionalBranch && instructionCode.Contains("true");
        }
        
        
    }
}