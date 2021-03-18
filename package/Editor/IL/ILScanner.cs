using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Disassembler.Editor.IL.OpCodesInfo;
using Disassembler.Editor.Plugins.SDILReader;
using UnityEngine;

namespace Disassembler.Editor.IL
{
    public class ILMethodInfo
    {
        public readonly IReadOnlyList<LocalVariableInfo> LocalVariables;
        public readonly IReadOnlyList<ILInstructionInfo> Instructions;

        public ILMethodInfo(IReadOnlyList<LocalVariableInfo> localVariables, IReadOnlyList<ILInstructionInfo> instructions)
        {
            LocalVariables = localVariables;
            Instructions = instructions;
        }
        
        
    }
    
    public class ILScanner
    {
        public static bool IsFieldAccessed(Type type, string name)
        {
            if (type == null) return false;
            if (string.IsNullOrWhiteSpace(name)) return false;
            
            var methods = GetMethods(type);
            return methods.Any(m => IsFieldAccessed(m, name));
        }

        public static bool IsFieldAccessed(MethodInfo method, string name)
        {
            var info = Disassemble(method);
            if (info == null) return false;
            foreach (var instr in info.Instructions)
            {
                if (instr.Operand == null) continue;
                if (instr.Operand is FieldInfo field)
                {
                    if (field.Name == name)
                    {
//                        Debug.Log("found field access");
                        return true;
                    }
                }
//                else if (instr.Operand is MethodInfo calledMethod)
//                {
//                    Debug.Log("called " + calledMethod.Name);
//                }
            }

            return false;
        }
        
        public static IEnumerable<MethodInfo> GetMethods(object obj,
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        {
            return obj == null ? null : GetMethods(obj.GetType(), flags);
        }

        public static IEnumerable<MethodInfo> GetMethods(Type type,
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        {
            return type.GetMethods(flags);
        }

        public static ILMethodInfo Disassemble(MethodInfo method)
        {
            if (method == null) return null;
            
            var reader = new MethodBodyReader(method);
            var ilCode = reader.GetBodyCode();
            
            if (string.IsNullOrWhiteSpace(ilCode))
                return null;

            var localVariables = new List<LocalVariableInfo>();
            if (reader.LocalVariables != null) localVariables.AddRange(reader.LocalVariables);

            var lines = ilCode?.Split('\n').ToList();
            lines.RemoveAll(string.IsNullOrWhiteSpace);
            var ilCodeLines = lines.ToArray();

            var ilLinesInfos = new OpCodeInfo[ilCodeLines?.Length ?? 0];
            if (reader.instructions == null) return null;
            for (var index = 0; index < reader.instructions.Count && index < ilLinesInfos.Length; index++)
            {
                var instruction = reader.instructions[index];
                var d = ILNavigator.TryGetOpCodeDescription(instruction.Code);
                if (d == null) continue;
                ilLinesInfos[index] = d;
            }

            var instructions = FindBlocks(ilCodeLines, reader.instructions, ilLinesInfos);
            return new ILMethodInfo(localVariables, instructions);
        }
        
        
        
        // TODO refactor to work without lines list because we already have all info in instructions
        private static List<ILInstructionInfo> FindBlocks(IReadOnlyList<string> lines, IReadOnlyList<ILInstruction> instructions, IReadOnlyList<OpCodeInfo> lineInfos)
        {
            var instructionInfos = new List<ILInstructionInfo>();
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var info = i < lineInfos.Count ? lineInfos[i] : null;
                var instruction = i < instructions.Count ? instructions[i] : null;
                if (instruction == null) Debug.LogWarning("No instruction for il line " + line + " index: " + i + " something is wrong");
                instructionInfos.Add(new ILInstructionInfo(line, instruction, info));
            }

            foreach (var info in instructionInfos)
                info.Resolve(instructionInfos);
            return instructionInfos;
        }
        
        
        
    }
}