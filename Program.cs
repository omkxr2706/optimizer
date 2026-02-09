using System;
using System.IO;
using System.Text;

namespace HtmlOptimizer
{
    internal static class Program
    {
        private static readonly string[] Folders = { "CD", "cdcoapp", "consent" };

        // Usage:
        //   dotnet run
        //      -> Optimizes all HTML-ish files from input/<folder> into output/<folder>
        //
        // Optional:
        //   dotnet run file "C:\path\to\something.html-paste.txt"
        //      -> Optimizes just that file into output next to it (or mirrored into output if under input)
        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var projectDir = Directory.GetCurrentDirectory();
            var baseInputDir = Path.Combine(projectDir, "input");
            var baseOutputDir = Path.Combine(projectDir, "output");

            Directory.CreateDirectory(baseInputDir);
            Directory.CreateDirectory(baseOutputDir);

            if (args.Length >= 2 && args[0].Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                OptimizeSinglePath(baseInputDir, baseOutputDir, args[1]);
                return;
            }

            Console.WriteLine("HTML Optimizer (HTML only)\n");

            foreach (var folder in Folders)
            {
                var inDir = Path.Combine(baseInputDir, folder);
                var outDir = Path.Combine(baseOutputDir, folder);

                Directory.CreateDirectory(inDir);
                Directory.CreateDirectory(outDir);

                Console.WriteLine($"--- {folder} ---");
                Console.WriteLine($"Input : {inDir}");
                Console.WriteLine($"Output: {outDir}");

                new HtmlFileOptimizer(inDir, outDir).OptimizeAllFiles();
                Console.WriteLine();
            }

            Console.WriteLine("PDF step (manual):");
            Console.WriteLine("- Open output HTML in Chrome -> Ctrl+P -> Save as PDF");
            Console.WriteLine("- Paper: A4, Margins: Default, Headers/footers: OFF");
        }

        private static void OptimizeSinglePath(string baseInputDir, string baseOutputDir, string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"ERROR: File not found: {filePath}");
                return;
            }

            var full = Path.GetFullPath(filePath);
            var inputRoot = Path.GetFullPath(baseInputDir);

            string outDir;
            if (full.StartsWith(inputRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var rel = Path.GetRelativePath(baseInputDir, Path.GetDirectoryName(full)!);
                outDir = Path.Combine(baseOutputDir, rel);
            }
            else
            {
                outDir = Path.Combine(Path.GetDirectoryName(full)!, "output");
            }

            Directory.CreateDirectory(outDir);

            Console.WriteLine("HTML Optimizer (single file)\n");
            Console.WriteLine($"Input : {full}");
            Console.WriteLine($"Output: {outDir}\n");

            new HtmlFileOptimizer(Path.GetDirectoryName(full)!, outDir).OptimizeSpecificFile(full);
        }
    }
}
