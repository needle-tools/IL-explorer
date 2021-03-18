using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using Disassembler.Editor.Helper;
using UnityEngine;

namespace Disassembler.Editor.IL.OpCodesInfo
{
    [System.Serializable]
    public class OpCodeInfo
    {
        public string Opcode;
        public string Instruction;
        public string Description;
        public string TypeOfInstruction;

    }
    
    
    [System.Serializable]
    public class OpCodesInfo
    {
        public OpCodeInfo[] Descriptions; // serialized from json
        
        private readonly Dictionary<string, OpCodeInfo> codesDict = new Dictionary<string, OpCodeInfo>();

        public static OpCodesInfo Load()
        {
            try
            {
                var path = AssetDatabaseHelper.RelativeToScript(nameof(OpCodesInfo), "ilopcodes_v2.json");
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<OpCodesInfo>(json);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            return null;
        }
        
        public OpCodeInfo FindDescription(OpCode code)
        {
            
            // lazy initialize dictionary:
            if (Descriptions != null && Descriptions.Length != codesDict.Count)
            {
                string GetDictionaryKey(OpCodeInfo info) => info.Instruction.Split(' ').First();
                codesDict.Clear();
                foreach (var d in Descriptions)
                {
                    var key = GetDictionaryKey(d);
                    if (codesDict.ContainsKey(key) == false)
                        codesDict.Add(key, d);
                    else 
                        Debug.LogWarning("already added " + d.Instruction);
                }
            }

            var str = code.ToString();
            return codesDict.ContainsKey(str) ? codesDict[str] : TryResolve(code);
        }

        private OpCodeInfo TryResolve(OpCode code)
        {
//            foreach (var kvp in codesDict)
//            {
//                
//            }
            return null;
        }
    }
}