namespace Disassembler.Editor.IL.Utils
{
    public static class StringHelper
    {
        public static string I(this string str)
        {
            return "<i>" + str + "</i>";
        }
        
        public static string Col(this string str, string hex)
        {
            return "<color=" + hex + ">" + str + "</color>";
        }
        
        public static string B(this string str)
        {
            return "<b>" + str + "</b>";
        }
    }
}