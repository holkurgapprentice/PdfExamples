using System;
using System.IO;
using System.Linq;
using NReco.PdfGenerator;

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

			var headerHtml = WrapForNReco(File.ReadAllText(Path.Combine(paths.AssetsPath, "header.html")));
			var footerHtml = CreateFooterWithPageNumberScript(paths.AssetsPath);
			var bodyHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));

			var converter = new HtmlToPdfConverter
			{
				Size = PageSize.Letter,
				Orientation = PageOrientation.Portrait,
				PageHeaderHtml = headerHtml,
				PageFooterHtml = footerHtml
			};

			var pdfBytes = converter.GeneratePdf(bodyHtml);
			Directory.CreateDirectory(Path.GetDirectoryName(paths.OutputPath)!);
			File.WriteAllBytes(paths.OutputPath, pdfBytes);
			Console.WriteLine($"PDF saved to: {paths.OutputPath}");

			PrintMemoryUsage(startMemory);

			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] NReco failed: {ex.GetType().Name}: {ex.Message}");
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

			var headerHtml = WrapForNReco(File.ReadAllText(Path.Combine(paths.AssetsPath, "header.html")));
			var footerHtml = CreateFooterWithPageNumberScript(paths.AssetsPath);
			var bodyHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));

			var converter = new HtmlToPdfConverter
			{
				Size = PageSize.Letter,
				Orientation = PageOrientation.Portrait,
				PageHeaderHtml = headerHtml,
				PageFooterHtml = footerHtml
			};

			int successCount = 0;
			for (int i = 0; i < outputPaths.Length; i++)
			{
				try
				{
					var outputPath = outputPaths[i];
					Console.WriteLine($"Generating PDF {i + 1}/{outputPaths.Length}: {outputPath}");

					var pdfBytes = converter.GeneratePdf(bodyHtml);
					Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
					File.WriteAllBytes(outputPath, pdfBytes);
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
			Console.Error.WriteLine($"[ERROR] Sequential NReco failed: {ex.GetType().Name}: {ex.Message}");
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
		var outputPath = Path.GetFullPath(outputPathArg ?? Path.Combine(projectDirectory, "output-NReco.pdf"));
		var assetsPath = Path.Combine(projectDirectory, "..", "assets");

		return (outputPath, assetsPath);
	}

	private static string CreateFooterWithPageNumberScript(string assetsPath)
	{
		var footerContent = File.ReadAllText(Path.Combine(assetsPath, "footer.html"));
		return $@"<!DOCTYPE html>
<html><head>
<meta charset='utf-8'>
<script>
function subst() {{
  var vars = {{}};
  var query_strings_from_url = document.location.search.substring(1).split('&');
  for (var i = 0; i < query_strings_from_url.length; i++) {{
    var param = query_strings_from_url[i].split('=', 2);
    if (param.length == 1)
      vars[param[0]] = '';
    else
      vars[param[0]] = decodeURIComponent(param[1].replace(/\+/g, ' '));
  }}
  var pageNumEls = document.getElementsByClassName('pageNumber');
  var totalPagesEls = document.getElementsByClassName('totalPages');
  for (var i = 0; i < pageNumEls.length; ++i) pageNumEls[i].textContent = vars.page;
  for (var i = 0; i < totalPagesEls.length; ++i) totalPagesEls[i].textContent = vars.topage;
}}
</script>
</head><body onload='subst()'>
{footerContent}
</body></html>";
	}

	private static string WrapForNReco(string htmlFragment)
	{
		return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head><body>
{htmlFragment}
</body></html>";
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
