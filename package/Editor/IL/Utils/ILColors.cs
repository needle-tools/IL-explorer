namespace Disassembler.Editor.IL.Utils
{
    public class ILColors
    {
        private static string[] VariableColorCodes = new string[] {"#92c25b", "#62d9bf", "#867011", "#ccaa16", "#8e84f5", "#e19cff"};

        public static string VarHexCol(int index) => VariableColorCodes[index % VariableColorCodes.Length];
    }
}