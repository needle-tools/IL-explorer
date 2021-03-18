using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disassembler.Editor.IL
{
    [System.Serializable]
    public class ILNavigatorNavEntry
    {
        public string AssemblyFullName => assemblyFullName;
        public string TypeFullName => typeFullName;
        public string MethodFullName => methodFullName;
        public int LineOffset => lineOffset;

        [SerializeField] private string assemblyFullName;
        [SerializeField] private string typeFullName;
        [SerializeField] private string methodFullName;
        [SerializeField] private int lineOffset;

        public bool HasLineOffset => lineOffset >= 0;

        public ILNavigatorNavEntry(string assemblyFullName, string typeFullName, string methodFullName, int lineOffset = -1)
        {
            this.assemblyFullName = assemblyFullName;
            this.typeFullName = typeFullName;
            this.methodFullName = methodFullName;
            this.lineOffset = lineOffset;
        }
    }

    [System.Serializable]
    public class ILNavigatorHistory
    {
        [SerializeField] private int index;
        [SerializeField] private List<ILNavigatorNavEntry> history = new List<ILNavigatorNavEntry>();

        public ILNavigatorHistory(ILNavigatorNavEntry start)
        {
            this.history.Add(start);
            this.index = 0;
        }

        public void Add(DissassemblerSelectionInfo selection, int offset = -1)
        {
            if (this.MatchesCurrentIndex(selection.selectedAssembly, selection.selectedType, selection.selectedMethod, offset))
                return;
            
            if (!this.AtEnd)
            {
                for (var i = this.history.Count - 1; i > index; i--)
                    history.RemoveAt(i);
            }
            
            this.history.Add(new ILNavigatorNavEntry(selection.selectedAssembly, selection.selectedType, selection.selectedMethod, offset));
            this.index = this.history.Count - 1;
        }

        public int Index => index;
        public ILNavigatorNavEntry Current
        {
            get
            {
                index = Mathf.Clamp(index, 0, history.Count-1);
                return history.Count > 0 ? history[index] : null;
            }
        }

        public bool AtStart => index <= 0;
        public bool AtEnd => index >= history.Count - 1;

        public void RemoveCurrent()
        {
            if (Current == null) return;
            this.history.RemoveAt(index);
            index -= 1;
            index = Mathf.Clamp(index, 0, history.Count - 1);
        }

        public ILNavigatorNavEntry NavigateForward()
        {
            if (!AtEnd) 
                index += 1;
            index = Mathf.Clamp(index, 0, history.Count - 1);
            return Current;
        }

        public ILNavigatorNavEntry NavigateBackward()
        {
            if (!AtStart) 
                index -= 1;
            index = Mathf.Clamp(index, 0, history.Count - 1);
            
            return Current;
        }

        private bool MatchesCurrentIndex(string assembly, string type, string method, int offset)
        {
            var cur = this.Current;
            var sameAssembly = string.Equals(assembly, cur.AssemblyFullName, StringComparison.InvariantCultureIgnoreCase);
            var sameType = string.Equals(type, cur.TypeFullName, StringComparison.InvariantCultureIgnoreCase);
            var sameMethod = string.Equals(method, cur.MethodFullName, StringComparison.InvariantCultureIgnoreCase);
            var sameOffset = offset == cur.LineOffset;
            return sameAssembly && sameType && sameMethod && sameOffset;
        }
    }
}