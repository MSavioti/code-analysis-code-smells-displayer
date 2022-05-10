using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TP11
{
    public class CsharpFileContent : IComparable<CsharpFileContent>, IComparable
    {
        public string Name { get; set; }
        public List<CodeSmell> Smells { get; }
        public string Path => _path;
        private readonly string _path;

        public struct CodeSmell
        {
            public ECodeSmellType CodeSmellType;
            public int Line;

            public CodeSmell(ECodeSmellType codeSmellType, int line)
            {
                CodeSmellType = codeSmellType;
                Line = line;
            }
        }

        public CsharpFileContent(string path)
        {
            _path = path;
            Smells = new List<CodeSmell>();
        }

        public int GetSmellCount()
        {
            return Smells?.Count ?? 0;
        }

        public int GetSmellCount(ECodeSmellType codeSmellType)
        {
            var count = 0;

            foreach (var smell in Smells)
            {
                if (smell.CodeSmellType == codeSmellType)
                    count++;
            }

            return count;
        }

        public override string ToString()
        {
            var fileName = System.IO.Path.GetFileName(Path);
            return $"{fileName}: {GetSmellCount()} smells.";
        }

        public int CompareTo(CsharpFileContent other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return string.Compare(_path, other._path, StringComparison.Ordinal);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is CsharpFileContent other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(CsharpFileContent)}");
        }
    }
}
