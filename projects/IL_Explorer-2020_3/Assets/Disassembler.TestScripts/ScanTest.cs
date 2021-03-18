using System;
using System.Collections.Generic;
using Disassembler.Editor.IL;
using UnityEngine;
using Object = System.Object;

namespace Disassembler.TestScripts 
{
    public class ScanTest : MonoBehaviour 
    { 
        public float MyValue;
        public bool MyValueIsAccessed;

        public Component FieldNameOwner;
        public string FieldNameToScan;
        public bool FieldNameToScanIsAccessed;
        
        public void OnValidate()
        {
            MyValue = 5; 
            var scanner = new ILScanner();
            MyValueIsAccessed = ILScanner.IsFieldAccessed(GetType(), nameof(MyValue));

            FieldNameToScanIsAccessed = ILScanner.IsFieldAccessed(FieldNameOwner?.GetType(), FieldNameToScan);
            

        }


        [ContextMenu(nameof(PrintMethods))]
        private void PrintMethods()
        {
            if (!FieldNameOwner) return;
            var methods = ILScanner.GetMethods(FieldNameOwner);
            foreach (var m in methods)
                Debug.Log(m.Name);
        }

    }
}