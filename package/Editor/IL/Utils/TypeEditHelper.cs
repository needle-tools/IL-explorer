using System;
using Disassembler.Editor.Helper;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Disassembler.Editor.IL.Utils
{
    public class TypeEditHelper
    {
        public static string TryFindPathToType(string typeName)
        {
            var scriptPath = AssetDatabaseHelper.FindScriptPath(typeName);
            return scriptPath;
        }

        public static void TryOpenTypeAsset(string assetPath, int lineNumber = 0)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                AssetDatabase.OpenAsset(asset, lineNumber);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
        
    }
}