using System.Collections.Generic;
using System.IO;
using System.Linq;
using Disassembler.Editor.IL.Utils;
using Disassembler.Editor.Plugins.AutocompleteSearchField;
using Disassembler.Editor.Plugins.SDILReader;
using UnityEditor;
using UnityEngine;

namespace Disassembler.Editor.IL.Views
{
    public class IlJumpConnection
    {
        public readonly ILInstructionInfo From;
        public readonly ILInstructionInfo To;
        public readonly Color LineColor;

        public IlJumpConnection(ILInstructionInfo from, ILInstructionInfo to)
        {
            From = from;
            To = to;
            LineColor = !from.IsConditionalBranch
                ? new Color(.5f, .5f, .5f, .5f)
                : from.Is_BrIfTrue
                    ? new Color(0.2f, .8f, .2f, .7f)
                    : new Color(1f, .2f, .2f, .5f);
        }

        public Vector2 FromPosition;
        public Vector2 ToPosition;

        public float GetTop() => Mathf.Min(this.FromPosition.y, this.ToPosition.y);
        public float GetBottom() => Mathf.Max(this.FromPosition.y, this.ToPosition.y);
        public float GetLength() => Mathf.Abs(GetTop() - GetBottom());
    }

    public class ILWindow : EditorWindow
    {
        // Add menu named "My Window" to the Window menu
        [MenuItem("Window/Analysis/IL Explorer", false, 1000)]
        private static void Init()
        {
            Globals.LoadOpCodes();
            var window = (ILWindow) GetWindow(typeof(ILWindow), false, "IL Explorer", true);
            window.Show();
        }

        public ILNavigator CurrentModel => Model;

        private ILNavigator model;

        private ILNavigator Model
        {
            get
            {
                if (model != null) return model;

                model = ILNavigator.GetDisassembler("IL-Explorer");
                model.Updated += OnModelChanged;
                model.DisassemblyChanged += OnDisassemblyChanged;
                model.Navigated += OnNavigated;
                model.ReloadAssemblies();
                return model;
            }
        }

        public event System.Action<ILInstructionInfo> InstructionHoverChanged;

        public ILInstructionInfo LastHoveredInstruction {
            get => lastHoveredInstruction; 
            private set {
                if(lastHoveredInstruction != value) {
                    lastHoveredInstruction = value;
                    if(InstructionHoverChanged != null)
                        InstructionHoverChanged(lastHoveredInstruction);
                }
            }
        }

        private ILInstructionInfo lastHoveredInstruction;
        private void OnNavigated(ILNavigator obj)
        {
            jumps.Clear();
            asmSearch.searchString = Model.SelectedAssemblyDisplayName;
            typeSearch.searchString = Model.SelectedTypeDisplayName;
            methodSearch.searchString = Model.SelectedMethodDisplayName;
            GetCurrentJumpData();
            EditorUtility.SetDirty(this);
        }

        private void OnModelChanged(ILNavigator ilNavigator)
        {
            if (!this || jumps == null || asmSearch == null || typeSearch == null || methodSearch == null)
            {
                // Debug.LogWarning("ILWindow is destroyed but still calling OnModelChanged");
                return;
            }
            jumps.Clear();
            asmSearch.searchString = Model.SelectedAssemblyDisplayName;
            typeSearch.searchString = Model.SelectedTypeDisplayName;
            methodSearch.searchString = Model.SelectedMethodDisplayName;
            EditorUtility.SetDirty(this);
        }

        private void OnDisassemblyChanged(ILNavigator ild)
        {
            GetCurrentJumpData();
        }

        private void GetCurrentJumpData()
        {
            jumps.Clear();
            for (var i = 0; i < Model.IlInstructionInfoCount; i++)
            { 
                var instr = Model.GetInstructionInfo(i);
                foreach (var other in instr.JumpsToOtherLineses)
                {
                    jumps.Add(new IlJumpConnection(instr, other));
                }
            }

            jumpLinesNeedRecalculation = true;
        }


        [SerializeField] private bool autoDisassemble = true;
        [SerializeField] private AutocompleteSearchField asmSearch, typeSearch, methodSearch;
        private bool statisticsFoldout = false;
        private Vector2 scroll;

        #region Lifecycle

        private void OnEnable()
        {
            titleContent = new GUIContent("IL Explorer");
            
            GetCurrentJumpData();

            // UI Setup
            if (asmSearch == null)
                asmSearch = new AutocompleteSearchField();
            asmSearch.searchString = Model.SelectedAssemblyDisplayName;
            asmSearch.ClearResults();
            asmSearch.onInputChanged = (assemblyName) =>
            {
                asmSearch.ClearResults();
                if (string.IsNullOrEmpty(assemblyName))
                    foreach (var an in Model.AssemblyNames)
                        asmSearch.AddResult(an);
                else
                    foreach (var an in Model.AssemblyNames.Where(x => x.ToLowerInvariant().Contains(assemblyName.ToLowerInvariant())))
                        asmSearch.AddResult(an);
            };
            asmSearch.onConfirm = (assemblyName) =>
            {
                Model.SelectAssembly(assemblyName);
                asmSearch.ClearResults();
                typeSearch.searchString = "";
                methodSearch.searchString = "";
                typeSearch.onInputChanged("");
            };


            if (typeSearch == null)
                typeSearch = new AutocompleteSearchField();
            typeSearch.searchString = Model.SelectedTypeDisplayName;
            typeSearch.ClearResults();
            typeSearch.onInputChanged = (typeName) =>
            {
                typeSearch.ClearResults();
                if (string.IsNullOrEmpty(typeName))
                    foreach (var an in Model.TypeNames)
                        typeSearch.AddResult(an);
                else
                    foreach (var an in Model.TypeNames.Where(x => x.ToLowerInvariant().Contains(typeName.ToLowerInvariant())))
                        typeSearch.AddResult(an);
            };
            typeSearch.onConfirm = (typeName) =>
            {
                Model.SelectType(typeName);
                typeSearch.ClearResults();
                methodSearch.searchString = "";
                methodSearch.onInputChanged("");
            };


            if (methodSearch == null)
                methodSearch = new AutocompleteSearchField();
            methodSearch.searchString = Model.SelectedMethodDisplayName;
            methodSearch.ClearResults();
            methodSearch.onInputChanged = (methodName) =>
            {
                methodSearch.ClearResults();
                if (string.IsNullOrEmpty(methodName))
                    foreach (var an in Model.MethodNames)
                        methodSearch.AddResult(an);
                else
                    foreach (var an in Model.MethodNames.Where(x => x.ToLowerInvariant().Contains(methodName.ToLowerInvariant())))
                        methodSearch.AddResult(an);
            };
            methodSearch.onConfirm = (methodName) =>
            {
                Model.SelectMethod(methodName);
                methodSearch.ClearResults();
                Model.DisassembleMethod();
                Model.SaveNavigation();
            };
            methodSearch.onSelectedIndexChanged = (methodName) =>
            {
                Model.SelectMethod(methodName);
                Model.DisassembleMethod();
            };
        }

        public void OnDisable()
        {
            model.Updated -= OnModelChanged;
            model.DisassemblyChanged -= OnDisassemblyChanged;
            model.Navigated -= OnNavigated;
            CleanupFont();
        }

        #endregion

        private readonly List<IlJumpConnection> jumps = new List<IlJumpConnection>();
        private bool jumpLinesNeedRecalculation = false;

        private readonly Dictionary<ILInstructionInfo, Rect> ilCodePositions = new Dictionary<ILInstructionInfo, Rect>();
        private Rect ilCodeRect;

        private string ilSearch = "Search in IL";
        private readonly HashSet<ILInstructionInfo> searchMatches = new HashSet<ILInstructionInfo>();
        private bool didJumpToSearchResult = false;

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Assembly From File"))
                Model.LoadAssemtlyFromFile();
            if (GUILayout.Button("Refresh Assemblies"))
                Model.ReloadAssemblies();
            GUILayout.EndHorizontal();

            asmSearch.OnGUI();
            typeSearch.OnGUI();
            methodSearch.OnGUI();

            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!Model.CanNavigateBack))
                if (GUILayout.Button("Prev"))
                    Model.NavigateBack();
            if (GUILayout.Button("Disassemble"))
                Model.DisassembleMethod();
            using (new EditorGUI.DisabledScope(!Model.CanNavigateForward))
                if (GUILayout.Button("Next"))
                    Model.NavigateForward();
            GUILayout.EndHorizontal();

            var scriptPath = TypeEditHelper.TryFindPathToType(Model.SelectedTypeDisplayName);
            var hasFoundPath = !string.IsNullOrEmpty(scriptPath);
            // only show the open button if the type is in our database
            if (hasFoundPath)
            {
//                EditorGUI.BeginDisabledGroup(!hasFoundPath); // this is for disabling the button when we cant edit the script but not showing it at all is better
                var buttonText = "Open " + Path.GetFileName(scriptPath); // hasFoundPath ? "Open " + Path.GetFileName(scriptPath) : "Not in Database";
                if (GUILayout.Button(buttonText)) TypeEditHelper.TryOpenTypeAsset(scriptPath);
//                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space();
            statisticsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(statisticsFoldout, "Statistics");
            if (statisticsFoldout)
            {
                autoDisassemble = EditorGUILayout.Toggle("Auto disassemble on method selection", autoDisassemble);
                EditorGUILayout.IntField("Assembly Count", Model.AssemblyCount);
                EditorGUILayout.Space();
                EditorGUILayout.IntField("Types in selected asm", Model.TypeCount);
                EditorGUILayout.IntField("Methods in selected type", Model.MethodCount);
                EditorGUILayout.Space();
                EditorGUILayout.IntField("IL Binary Size", Model.ByteCount);
                EditorGUILayout.IntField("IL Instruction Count", Model.IlInstructionInfoCount);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.EndVertical();

            GUILayout.Space(10);
            DrawAndHandleSearch();

            ilCodePositions.Clear();

            VerifyFont();

            var style = new GUIStyle(GUI.skin.label) {richText = true, alignment = TextAnchor.UpperLeft};
            var foundStyle = new GUIStyle(style) {normal = {textColor = new Color(0f, .2f, 1f)}};
            var descStyle = new GUIStyle(style) {normal = {textColor = new Color(.4f, .4f, .4f)}};
            var blockStyle = new GUIStyle(style) {normal = {textColor = new Color(.6f, .6f, .6f)}};
            var headerStyle = new GUIStyle(style) {fontSize = (int) (style.fontSize * 1f)};

            GUILayout.BeginVertical();
            scroll = GUILayout.BeginScrollView(scroll);
            var cullHeight = 0f;
            var headerSize = headerStyle.fontSize * 1.5f;
            cullHeight += headerSize;
            if (scroll.y < cullHeight)
                EditorGUILayout.LabelField(Model.SelectedMethodDisplayName.B(), headerStyle, GUILayout.ExpandWidth(true),
                    GUILayout.Height(headerSize));
            else
                GUILayout.Space(headerSize);

            var variablesHeight = Model.LocalVariables.Count() * 14;
            cullHeight += variablesHeight;
            // total height: 14 * Model.LocalVariables.Count
            if (scroll.y < cullHeight)
                foreach (var loc in Model.LocalVariables)
                {
                    if (loc == null) continue;
                    var typeName = loc?.LocalType?.Namespace + "." + loc?.LocalType?.Name;
                    var variableString = ("var." + loc?.LocalIndex).B() + " : " + typeName + (loc.IsPinned ? " (pinned)" : "");
                    variableString = variableString.Col(ILColors.VarHexCol(loc.LocalIndex));
                    EditorGUILayout.LabelField(variableString, style, GUILayout.ExpandWidth(true), GUILayout.Height(14f));
                }
            else
                GUILayout.Space(variablesHeight);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            var variableEnd = GUILayoutUtility.GetLastRect();
            var lineRect = new Rect(variableEnd) {height = 1};
            lineRect.y += 2;
            EditorGUI.DrawRect(lineRect, new Color(0, 0, 0, .1f));
            cullHeight = variableEnd.y;

            for (var index = 0; index < Model.IlInstructionInfoCount; index++)
            {
                var instructionInfo = Model.GetInstructionInfo(index);

                //var elementHeight = 14;
                //if (instructionInfo.IsBlockStart) elementHeight += 14;
                //cullHeight += elementHeight;

                //if(cullHeight < scroll.y)
                //{
                //    GUILayout.Space(elementHeight);
                //    var instructionRect2 = GUILayoutUtility.GetLastRect();
                //    ilCodePositions.Add(instructionInfo, instructionRect2);
                //    continue;
                //}

                var line = instructionInfo.Line;
                var info = instructionInfo.OpInfo;
                AppendVariableAccess(instructionInfo, ref line);

                // should probably ony be updated when search string updates ;)
                var lineStyle = searchMatches.Contains(instructionInfo) ? foundStyle : style;

                if (instructionInfo.IsBlockStart)
                {
                    EditorGUILayout.LabelField(
                        "-------------------------------------------------------------------------------------------------------------------------------------",
                        blockStyle, GUILayout.ExpandWidth(true), GUILayout.Height(14f));
                }

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(line, lineStyle, GUILayout.ExpandWidth(true), GUILayout.Height(14f));
                var instructionRect = GUILayoutUtility.GetLastRect();
                ilCodePositions.Add(instructionInfo, instructionRect);
                if (info != null && !string.IsNullOrWhiteSpace(info.Description))
                    EditorGUILayout.LabelField(info.Description, descStyle, GUILayout.ExpandWidth(true), GUILayout.Height(14f));
                GUILayout.EndHorizontal();

                // click on instruction:
                if(instructionRect.Contains(Event.current.mousePosition)) {
                    LastHoveredInstruction = instructionInfo;
                    if (Model.CanNavigateTo(instructionInfo)) {
                        EditorGUIUtility.AddCursorRect(instructionRect, MouseCursor.Link);
                        
                        if (Event.current.type == EventType.MouseUp) {
                            Model.TryNavigateTo(instructionInfo);
                        }
                    }
                }
            }

            // drawn inside scrollrect (so, in scrollrect coordinate space)
            RecalculateJumpLines();
            DrawJumpLines();

            GUILayout.Space(30);
            GUILayout.EndScrollView();
            var scrollRect = GUILayoutUtility.GetLastRect();
            if (scrollRect.height > 1)
                ilCodeRect = scrollRect;

            GUILayout.EndVertical();
        }

        private void AppendVariableAccess(ILInstructionInfo info, ref string line)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var varIndex in info.LocalVariableAccesses)
            {
                var var = Model.GetLocalVariableByIndex(varIndex);
                if (var == null) continue;
                line += ((" var." + varIndex).B() + " " + var.LocalType?.Name).Col(ILColors.VarHexCol(varIndex));
            }
        }

        #region JUMPS

        private class JumpLane
        {
            public JumpLane(IlJumpConnection jump)
            {
                this.Add(jump);
            }

            private readonly HashSet<IlJumpConnection> Connections = new HashSet<IlJumpConnection>();

            public void Add(IlJumpConnection jump)
            {
                if (Connections.Contains(jump)) return;
                Connections.Add(jump);
                currentBottom = jump.GetBottom();
                currentTop = jump.GetTop();
                var length = jump.GetLength();
                maxLength = Mathf.Max(maxLength, length);
            }

            public void SetLaneHorizontal(float offset)
            {
                foreach (var c in Connections)
                {
                    var from = c.FromPosition;
                    var to = c.ToPosition;
                    var x = offset;
                    to.x += x;
                    from.x += x;
                    
                    // Rounding so we get pixel-perfect drawing
                    // not sure where this half-pixel offset comes from, but it's necessary to draw single-pixel width lines
                    to.x = Mathf.RoundToInt(to.x) - 0.5f;
                    from.x = Mathf.RoundToInt(from.x) - 0.5f;
                    to.y = Mathf.RoundToInt(to.y);
                    from.y = Mathf.RoundToInt(from.y);

                    c.FromPosition = from;
                    c.ToPosition = to;
                }
            }

            private float currentTop = -1, currentBottom = -1;
            private float maxLength;

            public float LongestJump => maxLength;

            public bool Free(IlJumpConnection jump)
            {
                var bottom = jump.GetBottom();
                var top = jump.GetTop();
                return bottom > currentBottom && bottom > currentTop && top > currentTop && top > currentBottom;
            }
        }

        private void RecalculateJumpLines()
        {
            if (!jumpLinesNeedRecalculation) return;

            var lanes = new List<JumpLane>();
            var width = 0f;

            // assign positions
            for (var index = 0; index < jumps.Count; index++)
            {
                var jump = jumps[index];
                var from = jump.From;
                var to = jump.To;
                var fromPos = ilCodePositions[from];
                var toPos = ilCodePositions[to];
                jump.FromPosition = fromPos.center;
                jump.ToPosition = toPos.center;
                width = ilCodePositions[from].width;
            }

            // sort by length
            var ordered = jumps.OrderBy(j => j.GetLength()).Reverse().ToList();
            jumps.Clear();
            jumps.AddRange(ordered);

            for (var index = 0; index < jumps.Count; index++)
            {
                var jump = jumps[index];
                var foundFreeLane = false;
                foreach (var lane in lanes)
                {
                    if (!lane.Free(jump)) continue;
                    lane.Add(jump);
                    foundFreeLane = true;
                    break;
                }

                if (!foundFreeLane)
                    lanes.Add(new JumpLane(jump));
            }

//            lanes = lanes.OrderBy(l => l.LongestJump).ToList(); //.Reverse().ToList();
            var hw = width * .5f;
            for (var li = 0; li < lanes.Count; li++)
            {
                var lane = lanes[li];
                var t01 = (float) li / lanes.Count;
                lane.SetLaneHorizontal(t01 * hw);
            }
        }

        float Map(float val, float srcMin, float srcMax, float dstMin, float dstMax)
        {
            return (val - srcMin) / ( srcMax - srcMin) * (dstMax - dstMin) + dstMin;
        }

        private void DrawJumpLines()
        {
            if (Event.current.type != EventType.Repaint) return;

            var visibleMin = scroll.y;
            var visibleMax = scroll.y + ilCodeRect.height;

            // Test visiblity rect
            //Handles.color = Color.white;
            //Handles.DrawLine(new Vector3(ilCodeRect.width / 2, visibleMin + 10), new Vector3(ilCodeRect.width / 2, visibleMax - 10));

            foreach (var jump in jumps)
            {
                // cull lines that are not visible
                if (jump.GetBottom() < visibleMin || jump.GetTop() > visibleMax) continue;

                Handles.color = jump.LineColor;
                Handles.DrawLine(jump.FromPosition, jump.ToPosition);
               
                if(jump.FromPosition.y < visibleMin || jump.ToPosition.y > visibleMax)
                {
                    float yMin = Mathf.Max(jump.FromPosition.y, visibleMin);
                    float yMax = Mathf.Min(jump.ToPosition.y, visibleMax);

                    Handles.color = jump.LineColor;
                    var v1 = jump.FromPosition;
                    var v2 = jump.ToPosition;
                    v1.x -= 1;
                    v2.x -= 1;
                    v1.y = Map(yMin, jump.FromPosition.y, jump.ToPosition.y, yMin, yMax);
                    v2.y = Map(yMax, jump.FromPosition.y, jump.ToPosition.y, yMin, yMax);
                    Handles.DrawLine(v1, v2);
                    Handles.DrawLine(v1 + new Vector2(2,0), v2 + new Vector2(2, 0));
                }

                const float startWidth = 20;
                const float startHeight = 1f;
                EditorGUI.DrawRect(new Rect(jump.FromPosition.x - startWidth, jump.FromPosition.y - startHeight / 2f, startWidth, startHeight), jump.LineColor);
                const float endWidth = 5;
                const float endHeight = 5;
                EditorGUI.DrawRect(new Rect(jump.ToPosition.x - endWidth / 2f, jump.ToPosition.y - endHeight / 2f, endWidth, endHeight), jump.LineColor);
            }
        }

        #endregion


        #region SEARCH

        private void DrawAndHandleSearch()
        {
            EditorGUI.BeginChangeCheck();
            ilSearch = EditorGUILayout.TextField(ilSearch);
            var searchChanged = EditorGUI.EndChangeCheck();
            // during the frame when this changes rect.y is zero!?
            // maybe we need to clone the rect when storing it?
            if (searchChanged) didJumpToSearchResult = false;
            if (didJumpToSearchResult) return;
            searchMatches.Clear();
            UpdateSearchResult(ilSearch);
            // TODO: would be nice if search would check if results could be centered on window
            if (searchMatches.Count != 1) return;
            var match = searchMatches.First();
            if (match == null || !ilCodePositions.ContainsKey(match)) return;
            var rect = this.ilCodePositions[match];
            if (!(rect.y > 0)) return;
            didJumpToSearchResult = true;
            var scrollPosition = rect.y - 40;
            scroll.y = Mathf.Clamp(scrollPosition, 0, float.MaxValue);
        }

        // using this to scroll to search line
        private void UpdateSearchResult(string searchString)
        {
            searchMatches.Clear();
            for (var index = 0; index < Model.IlInstructionInfoCount; index++)
            {
                var instruction = Model.GetInstructionInfo(index);
                var isSearched = !string.IsNullOrWhiteSpace(searchString) && instruction.Line.ToLowerInvariant().Contains(searchString);
                if (isSearched)
                    searchMatches.Add(instruction);
            }
        }

        #endregion


        #region Font Setup

        private GUIStyle _fixedFontStyle;
        private Font _font;
        private const int FontSize = 12;

        private void VerifyFont()
        {
            if (_fixedFontStyle != null && _font != null) return;
            _fixedFontStyle = new GUIStyle(GUI.skin.label);
            var fontName = Application.platform == RuntimePlatform.WindowsEditor ? "Consolas" : "Courier";

            CleanupFont();
            _font = Font.CreateDynamicFontFromOSFont(fontName, FontSize);
            _fixedFontStyle.font = _font;
            _fixedFontStyle.fontSize = FontSize;
        }

        private void CleanupFont()
        {
            if (_font == null) return;
            DestroyImmediate(_font, true);
            _font = null;
        }

        #endregion
    }
}