using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TP11
{
    public static class SyntaxHelper
    {
        public static SyntaxTree ParseProgramFile(string filePath)
        {
            var streamReader = new StreamReader(filePath, Encoding.UTF8);
            return CSharpSyntaxTree.ParseText(streamReader.ReadToEnd());
        }

        public static string[] FindAllCsharpFiles(string path)
        {
            return Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
        }

        public static string[] FindAllCshtmlFiles(string path)
        {
            return Directory.GetFiles(path, "*.cshtml", SearchOption.AllDirectories);
        }
    }
}
