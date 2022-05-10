using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Path = SixLabors.ImageSharp.Drawing.Path;

namespace TP11
{
    public enum ECodeSmellType
    {
        MagicAttribute, LongMethod, DataClass, LongParamList
    }

    public class CodeSmellsAnalyzer
    {
        private readonly string _rootPath;
        private readonly string _imageOutput;
        private List<CsharpFileContent> _csharpFilesContents;
        private string[] _csharpFiles;
        private readonly string[] _smellAbbreviations = new []{"Met\nLong", "Mts\nPar", "Atrb\nMag", "Data\nClss"};
        private int _cellSize = 40;
        private const int LeftMargin = 350;
        private const int RightMargin = 25;
        private const int LowerMargin = 25;
        private const int UpperMargin = 225;
        private const int TextFittingExtension = 100;
        private const int SmellsCount = 4;

        public CodeSmellsAnalyzer(string projectRoot, string imageOutput)
        {
            _rootPath = projectRoot;
            _imageOutput = imageOutput;
            _csharpFilesContents = new List<CsharpFileContent>();
        }

        public void Run()
        {
            Console.WriteLine("_______________________");
            Console.WriteLine("| INICIANDO OPERAÇÕES |");
            Console.WriteLine("_______________________");

            CollectFilesContent();
            AnalyzeFiles();
            ShowResults();
        }

        private void CollectFilesContent()
        {
            Console.WriteLine($"\nBuscando por arquivos em {_rootPath}...");
            _csharpFiles = SyntaxHelper.FindAllCsharpFiles(_rootPath);

            foreach (var path in _csharpFiles)
            {
                Console.WriteLine($" - Encontrado arquivo em \"{path}\".");
                _csharpFilesContents.Add(new CsharpFileContent(path));
            }
        }

        private void AnalyzeFiles()
        {
            Console.WriteLine("\nAnalisando arquivos coletados...");
            var updatedFiles = new List<CsharpFileContent>();

            foreach (var content in _csharpFilesContents)
            {
                Console.WriteLine($" - Analisando arquivo em \"{content.Path}\".");
                var visitor = new CodeSmellVisitor(content);
                updatedFiles.Add(visitor.AnalyzeCodeSmells());
            }

            _csharpFilesContents = updatedFiles;
        }

        private void ShowResults()
        {
            Console.WriteLine("\nAnálise concluída.");
            foreach (var file in _csharpFilesContents)
            {
                Console.WriteLine($" - {file}");
            }

            _csharpFilesContents.Sort();
            Console.WriteLine("\nCriando representação gráfica da análise obtida...");
            DirectoryInfo info = new DirectoryInfo(_rootPath);
            var fileName = $"CodeSmellsView_{info.Name}.png";
            var filePath = $"{_imageOutput}\\{fileName}";
            
            using (Image image = new Image<Rgba32>(1024, 728))
            {
                var pen = new Pen(Color.Black, 2);
                image.Mutate(x => x.Clear(Color.White));
                IPathCollection pathCollection = new PathCollection(CreateTable(image));
                image.Mutate(x => x.Draw(pen, pathCollection));
                WriteFilesNames(image);
                WriteSmellsNames(image);
                DrawColoredBoxes(image, pen);
                image.SaveAsPng(filePath);
            }

            Console.WriteLine($"\nArquivo de visualização salvo em \"{filePath}\".\n");
        }

        private IEnumerable<IPath> CreateTable(Image image)
        {
            List<IPath> paths = new List<IPath>();
            var cellVerticalSize = (image.Width) / _csharpFilesContents.Count;

            var yPosition = UpperMargin;

            var expectedRightMargin = LeftMargin + (SmellsCount) * _cellSize;

            if (expectedRightMargin > image.Width - RightMargin)
                expectedRightMargin = image.Width - RightMargin;

            for (int i = 0; i < _csharpFilesContents.Count + 1; i++)
            {
                var startPoint = new PointF(LeftMargin - TextFittingExtension, yPosition);
                var endPoint = new PointF(expectedRightMargin, yPosition);
                var line = new LinearLineSegment(startPoint, endPoint);
                IPath path = new Path(line);
                paths.Add(path);
                yPosition += _cellSize;
            }

            var xPosition = LeftMargin;

            var expectedLowerMargin = UpperMargin + (_csharpFilesContents.Count) * _cellSize;

            if (expectedLowerMargin > image.Height - LowerMargin)
                expectedLowerMargin = image.Height - LowerMargin;

            for (int i = 0; i < SmellsCount + 1; i++)
            {
                var startPoint = new PointF(xPosition, UpperMargin - TextFittingExtension);
                var endPoint = new PointF(xPosition, expectedLowerMargin);
                var line = new LinearLineSegment(startPoint, endPoint);
                IPath path = new Path(line);
                paths.Add(path);
                xPosition += _cellSize;
            }

            return paths;
        }

        private void WriteFilesNames(Image image)
        {
            var options = new TextGraphicsOptions
            {
                TextOptions =
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    WrapTextWidth = LeftMargin - 35
                }
            };

            const int xPosition = 25;
            var yPosition = UpperMargin + _cellSize / 2;

            foreach (var fileContent in _csharpFilesContents)
            {
                var point = new PointF(xPosition, yPosition);
                var font = new Font(SystemFonts.Find("Arial"), 20, FontStyle.Regular);
                image.Mutate(x => x.DrawText(options, fileContent.Name, font, Color.DarkBlue, point));
                yPosition += _cellSize;
            }
        }

        private void WriteSmellsNames(Image image)
        {
            var options = new TextGraphicsOptions
            {
                TextOptions =
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    WrapTextWidth = _cellSize
                }
            };

            var yPosition = UpperMargin - _cellSize;
            var xPosition = LeftMargin;

            foreach (var abbreviation in _smellAbbreviations)
            {
                var point = new PointF(xPosition, yPosition);
                var font = new Font(SystemFonts.Find("Arial"), 16, FontStyle.Regular);
                image.Mutate(x => x.DrawText(options, abbreviation, font, Color.DarkBlue, point));
                xPosition += _cellSize;
            }
        }

        private void DrawColoredBoxes(Image image, Pen pen)
        {
            var smellTypes = new[]
            {
                ECodeSmellType.LongMethod, ECodeSmellType.LongParamList, ECodeSmellType.MagicAttribute, ECodeSmellType.DataClass
            };

            var greatestValue = FindGreatestSmellValue();
            var xPosition = LeftMargin;
            var yPosition = UpperMargin;
            var fillSize = _cellSize - pen.StrokeWidth * 2f;

            foreach (var file in _csharpFilesContents)
            {
                foreach (var smellType in smellTypes)
                {
                    var smellCount = file.GetSmellCount(smellType);
                    var proportionalValue = GetProportionalValue(smellCount, greatestValue);
                    var color = GetProportionalColor(proportionalValue);
                    var point = new PointF(xPosition, yPosition);
                    var rectangle = new RectangularPolygon(point.X, point.Y, fillSize, fillSize);
                    image.Mutate(x => x.Fill(color, rectangle));
                    xPosition +=  _cellSize;
                }

                yPosition += _cellSize;
                xPosition = LeftMargin;
            }
        }

        private float GetProportionalValue(float targetValue, float fullValue)
        {
            return targetValue / fullValue;
        }

        private Color GetProportionalColor(float proportionalValue)
        {
            /*proportionalValue = 1f - proportionalValue;

            float r = 0f;
            float g = 0f;

            if (proportionalValue < 0.5f)
            {
                r = 255f;
                g = (int) (255f * proportionalValue / 0.5f);
            }
            else
            {
                g = 255f;
                r = 255f - (int) (255f * (proportionalValue - 0.5f) / 0.5f);
            }

            return new Argb32(r, g, 0f);*/
            return new Argb32(2f * proportionalValue, (1 - proportionalValue), 0f);
        }

        private int FindGreatestSmellValue()
        {
            var smellTypes = new[]
            {
                ECodeSmellType.LongMethod, ECodeSmellType.LongParamList, ECodeSmellType.MagicAttribute, ECodeSmellType.DataClass
            };

            var greatestValue = 0;

            foreach (var file in _csharpFilesContents)
            {
                foreach (var smellType in smellTypes)
                {
                    var smellCount = file.GetSmellCount(smellType);

                    if (smellCount > greatestValue)
                        greatestValue = smellCount;
                }
            }

            return greatestValue;
        }
    }
}
