using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Disassembler.Editor.Helper;
using Disassembler.Editor.IL.OpCodesInfo;
using Disassembler.Editor.IL.Utils;
using Disassembler.Editor.Plugins.SDILReader;
using Disassembler.Editor.Plugins.System.Reflection.ExtensionMethods;
using UnityEditor;
using UnityEngine;

namespace Disassembler.Editor.IL
{
    [InitializeOnLoad]
    public class ILNavigator
    {
        #region STATIC

        public static ILNavigator GetDisassembler(string key)
        {
            if (!instances.ContainsKey(key))
                instances.Add(key, new ILNavigator(key));
            return instances[key];
        }

        public static OpCodeInfo TryGetOpCodeDescription(OpCode code)
        {
            if (infos == null)
                infos = OpCodesInfo.OpCodesInfo.Load();
            return infos?.FindDescription(code);
        }

        private static readonly Dictionary<string, ILNavigator> instances = new Dictionary<string, ILNavigator>();
        private static OpCodesInfo.OpCodesInfo infos;

        static ILNavigator()
        {
            Globals.LoadOpCodes();
        }

        #endregion


        public void ReloadAssemblies()
        {
            LoadAssemblies();
        }

        public bool LogActions = false;

        public event Action<ILNavigator> Updated;
        public event Action<ILNavigator> DisassemblyChanged;
        public event Action<ILNavigator> Navigated;

        public string SelectedAssemblyDisplayName => selectedAssemblyDisplayName;
        public string SelectedTypeDisplayName => selectedTypeDisplayName;
        public string SelectedMethodDisplayName => selectedMethodDisplayName;

        public IEnumerable<string> AssemblyNames => assemblyNames;
        public IEnumerable<string> TypeNames => typeNames;
        public IEnumerable<string> MethodNames => methodNames;
        public IEnumerable<LocalVariableInfo> LocalVariables => localVariables.Values;
        public bool HasLocalVariables => localVariables.Count > 0;
        public LocalVariableInfo GetLocalVariableByIndex(int index) => localVariables.ContainsKey(index) ? localVariables[index] : null;

        public int AssemblyCount => assemblyNames?.Count ?? 0;
        public int TypeCount => typeNames?.Count ?? 0;
        public int MethodCount => methodNames?.Count ?? 0;

        public Type SelectedType => selectedType;
        public MethodInfo SelectedMethod => selectedMethod;

        public int ByteCount => byteCount;
        public ILInstructionInfo GetInstructionInfo(int index) => index >= 0 && index <= this.instructionInfo.Count ? this.instructionInfo[index] : null;
        public int IlInstructionInfoCount => this.instructionInfo.Count;

        public bool CanNavigateTo(ILInstructionInfo instruction)
        {
            switch (instruction.OperandType)
            {
                case OperandType.InlineMethod:
                    return true;
                default:
                    return false;
            }
        }

        public void TryNavigateTo(ILInstructionInfo instruction)
        {
            Debug.Log("CLICKED " + instruction.Line);
            switch (instruction.OperandType)
            {
                case OperandType.InlineMethod:
                    if (instruction.Operand is MethodInfo method)
                    {
                        var type = method.ReflectedType;
                        var methodName = method.GetSignature(false);
                        Debug.Log(("TryNavigate to " + type?.FullName + " : " + methodName + "\n" + type?.AssemblyQualifiedName).I());
                        // selecting assembly is not just the namespace
                        // like System.Collections.Generic is in System.Core Assembly -> so we would need a dictionary with all the types and their fullname 
                        // for all assemblies to really make sure we can navigate there
                        var assemblyId = type?.Assembly.FullName;
                        if (!SelectAssembly(assemblyId))
                        {
                            Debug.Log("could not find assembly at " + assemblyId);
                            return;
                        }

                        var typeId = type?.Namespace + "." + type?.Name;
                        if (!SelectType(typeId))
                        {
                            Debug.Log("Could not find type at " + typeId);
                            return;
                        }
                        
                        if (!SelectMethod(methodName)) 
                        {
                            Debug.Log("Could not find method at " + methodName);
                            return;
                        }

                        SaveNavigation();
                    }

                    break;
            }
        }

        public bool SelectAssembly(string assemblyName)
        {
            DoLog("Select assembly " + assemblyName);
            var assembly = TryFindAssembly(assemblyName);
            if (assembly == null) return false;
            selection.selectedAssembly = assembly.FullName;
            selectedAssembly = assembly;
            selectedAssemblyDisplayName = assembly.GetName().Name;
            DoLog("Selected Assembly: " + selectedAssembly);
            types = selectedAssembly.GetTypes();
            typeNames.Clear();
            foreach (var t in types)
                typeNames.Add(t.FullName);
            OnSelectedAssemblyChanged();
            Updated?.Invoke(this);
            return true;
        }

        public bool SelectType(string typeName, BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        {
            if (selectedAssembly == null && selection.selectedAssembly != null)
                SelectAssembly(selection.selectedAssembly);

            if (string.IsNullOrEmpty(typeName)) return false;
            var type = TryFindType(typeName);
            if (type == null) return false;
            selectedType = type;
            selection.selectedType = type.FullName;
            selectedTypeDisplayName = type.FullName;
            SaveSelection();
            this.methods = selectedType.GetMethods(flags);
            methodNames.Clear();
            foreach (var m in methods)
                methodNames.Add(m.GetSignature(false));
            OnSelectedTypeChanged();
            Updated?.Invoke(this);
            return true;
        }

        public void ClearSelectedType()
        {
            selectedType = null;
            selectedTypeDisplayName = "None";
            this.methods = null;
            methodNames.Clear();
            Updated?.Invoke(this);
        }

        public bool SelectMethod(string methodName)
        {
            if (selectedType == null && selection.selectedType != null)
                SelectType(selection.selectedType);

            if (string.IsNullOrEmpty(methodName)) return false;
            var method = TrySelectMethod(methodName);
            if (method == null) return false;
            selectedMethod = method;
            DoLog("Selected Method: " + selectedMethod);
            selection.selectedMethod = method.GetSignature(false);
            selectedMethodDisplayName = selection.selectedMethod;
            SaveSelection();
            if (!DisassembleMethod()) return false;
            Updated?.Invoke(this);
            return true;
        }

        public void ClearSelectedMethod()
        {
            selectedMethod = null;
            selectedMethodDisplayName = "None";
            byteCount = 0;
            ilCode = "";
            instructionInfo.Clear();
        }

        public bool DisassembleMethod()
        {
            if (selectedMethod == null && selection.selectedMethod != null)
                SelectMethod(selection.selectedMethod);

            if (selectedMethod == null) return false;
            var reader = new MethodBodyReader(selectedMethod);
            byteCount = reader.ilByteCount;
            ilCode = reader.GetBodyCode();
            localVariables.Clear();
            if (string.IsNullOrWhiteSpace(ilCode))
            {
                instructionInfo.Clear();
                Updated?.Invoke(this);
                return false;
            }

            if (reader.LocalVariables != null)
                foreach (var local in reader.LocalVariables)
                    localVariables.Add(local.LocalIndex, local);

            var lines = ilCode?.Split('\n').ToList();
            lines.RemoveAll(string.IsNullOrWhiteSpace);
            var ilCodeLines = lines.ToArray();

            var ilLinesInfos = new OpCodeInfo[ilCodeLines?.Length ?? 0];
            if (reader.instructions == null) return false;
            for (var index = 0; index < reader.instructions.Count && index < ilLinesInfos.Length; index++)
            {
                var instruction = reader.instructions[index];
                var d = TryGetOpCodeDescription(instruction.Code);
                if (d == null) continue;
                ilLinesInfos[index] = d;
            }

            FindBlocks(ilCodeLines, reader.instructions, ilLinesInfos);

            Updated?.Invoke(this);
            DisassemblyChanged?.Invoke(this);
            return true;
        }

        public void SaveNavigation()
        {
            if (this.navigation == null) this.CreateOrLoadHistory();
            this.navigation.Add(this.selection);
            this.SaveHistory();
        }

        public void NavigateBack()
        {
            if (this.navigation == null) this.CreateOrLoadHistory();
            this.navigation.NavigateBackward();
            this.SaveHistory();
            if (!TryRestoreFromNavigationHistory())
            {
                TryRestoreFromSelection();
            }
        }

        public bool CanNavigateBack => this.navigation != null && !this.navigation.AtStart;

        public void NavigateForward()
        {
            if (this.navigation == null) this.CreateOrLoadHistory();
            this.navigation.NavigateForward();
            this.SaveHistory();
            if (!TryRestoreFromNavigationHistory())
            {
                TryRestoreFromSelection();
            }
        }

        public bool CanNavigateForward => this.navigation != null && !this.navigation.AtEnd;

        private void FindBlocks(IReadOnlyList<string> lines, IReadOnlyList<ILInstruction> instructions, IReadOnlyList<OpCodeInfo> lineInfos)
        {
            instructionInfo.Clear();
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var info = i < lineInfos.Count ? lineInfos[i] : null;
                var instruction = i < instructions.Count ? instructions[i] : null;
                if (instruction == null) Debug.LogWarning("No instruction for il line " + line + " index: " + i + " something is wrong");
                instructionInfo.Add(new ILInstructionInfo(line, instruction, info));
            }

            foreach (var info in instructionInfo)
                info.Resolve(instructionInfo);
        }

        private Assembly TryFindAssembly(string assemblyName)
        {
            return assemblies?.FirstOrDefault(x => string.Equals(x.FullName, assemblyName, StringComparison.InvariantCultureIgnoreCase));
        }

        private Type TryFindType(string typeName)
        {
            return types?.FirstOrDefault(x => string.Equals(x.FullName, typeName, StringComparison.InvariantCultureIgnoreCase));
        }

        private MethodInfo TrySelectMethod(string methodName)
        {
            return methods?.FirstOrDefault(x => string.Equals(x.GetSignature(false), methodName, StringComparison.InvariantCultureIgnoreCase));
        }

        private bool _allowSaveState = true;

        private Assembly[] assemblies;
        private Type[] types;
        private MethodInfo[] methods;
        private readonly List<string> assemblyNames = new List<string>();
        private readonly List<string> methodNames = new List<string>();
        private readonly List<string> typeNames = new List<string>();

        private Assembly selectedAssembly;
        private Type selectedType;
        private MethodInfo selectedMethod;

        private DissassemblerSelectionInfo selection;
        private string selectedAssemblyDisplayName;
        private string selectedTypeDisplayName; // mainly for populating search fields
        private string selectedMethodDisplayName;

        private int byteCount;
        private string ilCode;
        private readonly Dictionary<int, LocalVariableInfo> localVariables = new Dictionary<int, LocalVariableInfo>();
        private readonly List<ILInstructionInfo> instructionInfo = new List<ILInstructionInfo>();

        private ILNavigatorHistory navigation;

        private void OnSelectedAssemblyChanged()
        {
            ClearSelectedType();
            ClearSelectedMethod();
        }

        private void OnSelectedTypeChanged()
        {
            ClearSelectedMethod();
        }

        private void OnSelectedMethodChanged()
        {
        }


        public ILNavigator(string key, bool allowSaveState = true)
        {
            this._allowSaveState = allowSaveState;
            CreateOrLoadSaveFile(key);
            CreateOrLoadHistory();
            LoadAssemblies();
            if (!TryRestoreFromNavigationHistory())
                TryRestoreFromSelection();
        }

        public void LoadAssemtlyFromFile()
        {
            var selectedPath = EditorUtility.OpenFilePanel("Select Assembly", Application.dataPath, "dll");
            LoadAssemblyAt(selectedPath);
        }

        public void LoadAssemblyAt(string path)
        {
            if (!File.Exists(path)) return;

            Debug.Log("not implemented");
//            var proxy = new DomainProxy("myproxy", path);
//            try
//            {
//                // just a test which doesnt work
////                var assembly = Assembly.Load(path);
////                this.assemblies = new[] {assembly};
////                assemblyNames.Clear();
////                foreach (var ass in assemblies)
////                    assemblyNames.Add(ass.GetName().FullName);
////                Updated?.Invoke(this);
//            }
//            catch (Exception e)
//            {
//                Debug.LogError(e);
//            }
        }


        private void LoadAssemblies()
        {
            var currentDomain = AppDomain.CurrentDomain;
            assemblies = currentDomain.GetAssemblies();
            assemblyNames.Clear();
            foreach (var assembly in assemblies)
                assemblyNames.Add(assembly.GetName().FullName);
            Updated?.Invoke(this);
        }

        private void TryRestoreFromSelection()
        {
            if (selection == null) return;
            SelectAssembly(selection.selectedAssembly);
            SelectType(selection.selectedType);
            SelectMethod(selection.selectedMethod);
        }

        private bool TryRestoreFromNavigationHistory()
        {
            var cur = navigation.Current;
            if (cur == null) return false;
            var assembly = cur.AssemblyFullName;
            var type = cur.TypeFullName;

            var method = cur.MethodFullName;
            // TODO: use line
            if (!SelectAssembly(assembly)) return false;
            if (!SelectType(type)) return false;
            if (!SelectMethod(method)) return false;
            SaveHistory();
            Navigated?.Invoke(this);
            return true;
        }


        #region PERSISTENT DATA

        private const string SaveFolderName = "ILDisassemblerData";
        private string GetPersistentFilePath(string fileName) => AssetDatabaseHelper.RelativeToScript(nameof(ILNavigator), SaveFolderName, fileName);


        private void CreateOrLoadHistory()
        {
            if (selection == null)
            {
                Debug.LogError("Selection must be loaded or created before creating navigation");
                return;
            }

            var filePath = GetPersistentFilePath(selection.key + "-Navigation.json");
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    this.navigation = JsonUtility.FromJson<ILNavigatorHistory>(json);
                }
                catch
                {
                    // ignored
                }
            }

            if (navigation != null) return;

            var start = new ILNavigatorNavEntry(this.selection.selectedAssembly, this.selection.selectedType, this.selection.selectedMethod);
            this.navigation = new ILNavigatorHistory(start);
            this.SaveHistory();
        }

        private void SaveHistory()
        {
            if (this.navigation == null)
                this.CreateOrLoadHistory();
            if (this.navigation == null) return;
            if (!this._allowSaveState) return;
            var filePath = GetPersistentFilePath(selection.key + "-Navigation.json");

            var dir = Path.GetDirectoryName(filePath);
            if (dir == null)
            {
                Debug.LogError("no filepath found for " + filePath);
                return;
            }

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonUtility.ToJson(this.navigation, true);
            File.WriteAllText(filePath, json);
        }

        private void CreateOrLoadSaveFile(string key)
        {
            var filePath = GetPersistentFilePath(key + ".json");
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    selection = JsonUtility.FromJson<DissassemblerSelectionInfo>(json);
                    DoLog("Loaded Selection\n" + json);
                }
                catch
                {
                    // ignored
                }
            }

            if (selection != null) return;
            selection = new DissassemblerSelectionInfo()
            {
                key = key
            };

            SaveSelection();
        }

        private void SaveSelection()
        {
            if (!this._allowSaveState) return;
            var filePath = GetPersistentFilePath(selection.key + ".json");

            var dir = Path.GetDirectoryName(filePath);
            if (dir == null)
            {
                Debug.LogError("no filepath found for " + filePath);
                return;
            }

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonUtility.ToJson(selection, true);
            File.WriteAllText(filePath, json);
        }

        #endregion

        private void DoLog(object msg)
        {
            if (this.LogActions)
                Debug.Log(msg);
        }
    }


    [System.Serializable]
    public class DissassemblerSelectionInfo
    {
        public string key;
        public string selectedAssembly;
        public string selectedType;
        public string selectedMethod;
    }
}