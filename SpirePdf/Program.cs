using System;
using System.IO;
using System.Linq;
using Spire.Pdf;
using Spire.Pdf.Graphics;
using System.Drawing;
using Spire.Additions.Qt;

class Program
{
	static int Main(string[] args)
	{
		try
		{
			// Check for sequential mode
			if (args.Length > 0 && args[0] == "--sequential")
			{
				return RunSequential(args.Skip(1).ToArray());
			}

			// Single PDF mode (existing functionality)
			var startMemory = MeasureCurrentMemory();
			var paths = ResolvePaths(args);

			var headerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "header.html"));
			var bodyHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));
			var footerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "footer.html"));

			var htmlString = headerHtml + bodyHtml + footerHtml;

			PdfDocument doc = new PdfDocument();

			doc.PageSettings.Size = PdfPageSize.Letter;
			doc.PageSettings.Orientation = PdfPageOrientation.Portrait;
			doc.PageSettings.Margins = new PdfMargins(15, 95, 15, 55); // left, top, right, bottom

			string pluginPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "plugins");
			HtmlConverter.PluginPath = pluginPath;

			HtmlConverter.Convert(htmlString, paths.OutputPath, true, 100000, new Size(1080, 1000), new PdfMargins(0), LoadHtmlType.SourceCode);

			Console.WriteLine($"PDF saved to: {paths.OutputPath}");

			PrintMemoryUsage(startMemory);

			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] Spire.PDF failed: {ex.GetType().Name}: {ex.Message}");
			Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
			return 1;
		}
	}

	static int RunSequential(string[] outputPaths)
	{
		try
		{
			var startMemory = MeasureCurrentMemory();
			var paths = ResolvePaths(new string[0]); // Use default path for assets

			Console.WriteLine($"Starting sequential PDF generation for {outputPaths.Length} PDFs at {DateTime.Now:O}");

			var headerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "header.html"));
			var bodyHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));
			var footerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "footer.html"));

			var htmlString = headerHtml + bodyHtml + footerHtml;

			PdfDocument doc = new PdfDocument();

			doc.PageSettings.Size = PdfPageSize.Letter;
			doc.PageSettings.Orientation = PdfPageOrientation.Portrait;
			doc.PageSettings.Margins = new PdfMargins(15, 95, 15, 55); // left, top, right, bottom

			string pluginPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "plugins");
			HtmlConverter.PluginPath = pluginPath;

			int successCount = 0;
			for (int i = 0; i < outputPaths.Length; i++)
			{
				try
				{
					var outputPath = outputPaths[i];
					Console.WriteLine($"Generating PDF {i + 1}/{outputPaths.Length}: {outputPath}");

					HtmlConverter.Convert(htmlString, outputPath, true, 100000, new Size(1080, 1000), new PdfMargins(0), LoadHtmlType.SourceCode);

					Console.WriteLine($"PDF saved to: {outputPath}");
					successCount++;
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"[ERROR] Failed to generate PDF {i + 1}/{outputPaths.Length}: {ex.GetType().Name}: {ex.Message}");
				}
			}

			PrintMemoryUsage(startMemory);
			Console.WriteLine($"Sequential generation completed: {successCount}/{outputPaths.Length} PDFs generated successfully.");

			return successCount == outputPaths.Length ? 0 : 1;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] Sequential Spire.PDF failed: {ex.GetType().Name}: {ex.Message}");
			Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
			return 1;
		}
	}

	private static (string OutputPath, string AssetsPath) ResolvePaths(string[] args)
	{
		var projectDirectory = Directory.GetParent(AppContext.BaseDirectory)!
			.Parent!
			.Parent!
			.Parent!
			.FullName;

		var outputPathArg = args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
		var outputPath = Path.GetFullPath(outputPathArg ?? Path.Combine(projectDirectory, "output-SpirePdf.pdf"));
		var assetsPath = Path.Combine(projectDirectory, "..", "assets");

		return (outputPath, assetsPath);
	}

	private static double MeasureCurrentMemory()
	{
		var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
		return currentProcess.WorkingSet64 / (1024.0 * 1024.0);
	}

	private static void PrintMemoryUsage(double startMemory)
	{
		var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
		currentProcess.Refresh();
		var endMemory = currentProcess.WorkingSet64 / (1024.0 * 1024.0);
		var peakMemory = currentProcess.PeakWorkingSet64 / (1024.0 * 1024.0);
		var memoryDelta = endMemory - startMemory;
		Console.WriteLine($"Memory usage: {endMemory:F2} MB (peak: {peakMemory:F2} MB, delta: {memoryDelta:F2} MB)");
	}
}
