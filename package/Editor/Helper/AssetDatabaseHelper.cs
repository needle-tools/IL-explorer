using System.IO;
using System.Linq;
using UnityEditor;

namespace Disassembler.Editor.Helper
{
    public static class AssetDatabaseHelper
    {
        public static string FindScriptPath(string scriptFileName)
        {
            var GUIDs = AssetDatabase.FindAssets(scriptFileName);
            return GUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(path => Path.GetExtension(path) == ".cs" && (Path.GetFileNameWithoutExtension(path) == scriptFileName || Path.GetFileName(path) == scriptFileName));
        }

        public static string RelativeToScript(string scriptfileName, params string[] path)
        {
            var scriptPath = FindScriptPath(scriptfileName);
            if (string.IsNullOrEmpty(scriptPath)) return null;
            var fullPath = Path.GetDirectoryName(scriptPath);
            return path.Aggregate(fullPath, Path.Combine);
        }

        public static T RelativeToScript<T>(string scriptfileName, params string[] path) where T : UnityEngine.Object
        {
            var fullPath = RelativeToScript(scriptfileName, path);
            return string.IsNullOrEmpty(fullPath) ? default : AssetDatabase.LoadAssetAtPath<T>(fullPath);
        }
    }
}