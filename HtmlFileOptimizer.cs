using System;
using System.IO;
using System.Linq;
using System.Text;

namespace HtmlOptimizer
{
    public sealed class HtmlFileOptimizer
    {
        private readonly string _inputDirectory;
        private readonly string _outputDirectory;

        public HtmlFileOptimizer(string inputDir, string outputDir)
        {
            _inputDirectory = inputDir;
            _outputDirectory = outputDir;
        }

        public void OptimizeAllFiles()
        {
            if (!Directory.Exists(_inputDirectory))
            {
                Console.WriteLine($"WARN: Input directory not found: {_inputDirectory}");
                return;
            }

            var files = Directory.GetFiles(_inputDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(IsCandidateInputFile)
                .ToArray();

            if (files.Length == 0)
            {
                Console.WriteLine($"WARN: No input files found in: {_inputDirectory}");
                Console.WriteLine("Expected: .html/.htm OR .txt containing HTML (including *.html-paste.txt).");
                return;
            }

            Directory.CreateDirectory(_outputDirectory);

            foreach (var file in files)
                OptimizeSpecificFile(file);

            Console.WriteLine($"OK: {_inputDirectory} -> {_outputDirectory}");
        }

        public void OptimizeSpecificFile(string filePath)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                var raw = File.ReadAllText(filePath, Encoding.UTF8);

                var title = AdvancedOptimizer.TitleFromFileName(fileName);
                var finalHtml = AdvancedOptimizer.BuildOptimizedHtml(raw, title, lang: "en");

                var outName = GetOutputHtmlName(fileName);
                var outputPath = Path.Combine(_outputDirectory, outName);

                File.WriteAllText(outputPath, finalHtml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                var originalKB = new FileInfo(filePath).Length / 1024;
                var optimizedKB = new FileInfo(outputPath).Length / 1024;
                var reduction = originalKB > 0 ? (100.0 * (originalKB - optimizedKB) / originalKB) : 0;

                Console.WriteLine($"{outName}: {originalKB} KB -> {optimizedKB} KB (-{reduction:F0}%)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private static bool IsCandidateInputFile(string path)
        {
            var name = Path.GetFileName(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext is ".html" or ".htm") return true;

            if (ext == ".txt")
            {
                // Your pattern: AppForm_Single_Assamese.html-paste.txt
                if (name.Contains(".html", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(".htm", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("paste", StringComparison.OrdinalIgnoreCase))
                    return true;

                // Also allow generic .txt (might still contain HTML)
                return true;
            }

            return false;
        }

        private static string GetOutputHtmlName(string inputName)
        {
            var lower = inputName.ToLowerInvariant();

            if (lower.EndsWith(".html") || lower.EndsWith(".htm"))
                return Path.ChangeExtension(inputName, ".html");

            var idx = lower.IndexOf(".html", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return inputName.Substring(0, idx + 5);

            idx = lower.IndexOf(".htm", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return inputName.Substring(0, idx + 4) + "l";

            return Path.GetFileNameWithoutExtension(inputName) + ".html";
        }
    }
}
