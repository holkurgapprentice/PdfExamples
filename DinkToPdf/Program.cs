using System.Runtime.Loader;
using System.Text;
using DinkToPdf;

internal class CustomAssemblyLoadContext : AssemblyLoadContext
{
	public IntPtr LoadUnmanagedLibrary(string absolutePath)
	{
		return LoadUnmanagedDll(absolutePath);
	}

	protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
	{
		return LoadUnmanagedDllFromPath(unmanagedDllName);
	}

	protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName)
	{
		return null;
	}
}

internal class Program
{
	private static int Main(string[] args)
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

			if (!ValidateWkHtmlToPdfExists(paths.WkHtmlToPdfPath))
			{
				return 1;
			}

			LoadWkHtmlToPdfLibrary(paths.WkHtmlToPdfPath);

			var tempDir = Path.Combine(Path.GetTempPath(), "DinkToPdf", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempDir);

			var converter = new SynchronizedConverter(new PdfTools());
			var doc = CreatePdfDocument(paths, tempDir);

			converter.Convert(doc);
			Console.WriteLine($"PDF saved to: {paths.OutputPath}");

			PrintMemoryUsage(startMemory);

			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] DinkToPdf failed: {ex.GetType().Name}: {ex.Message}");
			Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
			return 1;
		}
	}

	private static int RunSequential(string[] outputPaths)
	{
		try
		{
			var startMemory = MeasureCurrentMemory();
			var paths = ResolvePaths(new string[0]); // Use default path for assets

			if (!ValidateWkHtmlToPdfExists(paths.WkHtmlToPdfPath))
			{
				return 1;
			}

			LoadWkHtmlToPdfLibrary(paths.WkHtmlToPdfPath);

			Console.WriteLine($"Starting sequential PDF generation for {outputPaths.Length} PDFs at {DateTime.Now:O}");

			var converter = new SynchronizedConverter(new PdfTools());

			int successCount = 0;
			for (int i = 0; i < outputPaths.Length; i++)
			{
				try
				{
					var outputPath = outputPaths[i];
					Console.WriteLine($"Generating PDF {i + 1}/{outputPaths.Length}: {outputPath}");

					var tempDir = Path.Combine(Path.GetTempPath(), "DinkToPdf", Guid.NewGuid().ToString("N"));
					Directory.CreateDirectory(tempDir);

					var doc = CreatePdfDocumentWithOutputPath(paths, tempDir, outputPath);
					converter.Convert(doc);
					Console.WriteLine($"PDF saved to: {outputPath}");
					successCount++;

					// Clean up temp directory
					if (Directory.Exists(tempDir))
					{
						Directory.Delete(tempDir, true);
					}
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
			Console.Error.WriteLine($"[ERROR] Sequential DinkToPdf failed: {ex.GetType().Name}: {ex.Message}");
			Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
			return 1;
		}
	}

	private static (string OutputPath, string AssetsPath, string WkHtmlToPdfPath) ResolvePaths(string[] args)
	{
		var projectDirectory = Directory.GetParent(AppContext.BaseDirectory)!
			.Parent!
			.Parent!
			.Parent!
			.FullName;

		var outputPathArg = args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
		var outputPath = Path.GetFullPath(outputPathArg ?? Path.Combine(projectDirectory, "output-DinkToPdf.pdf"));
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

		var assetsPath = Path.Combine(projectDirectory, "..", "assets");
		var wkHtmlToPdfPath = Path.Combine(projectDirectory, "wkhtmltox.dll");

		return (outputPath, assetsPath, wkHtmlToPdfPath);
	}

	private static bool ValidateWkHtmlToPdfExists(string wkHtmlToPdfPath)
	{
		if (File.Exists(wkHtmlToPdfPath))
		{
			return true;
		}

		Console.Error.WriteLine($"[ERROR] wkhtmltox.dll not found at: {wkHtmlToPdfPath}");
		Console.Error.WriteLine("\nTo fix this:");
		Console.Error.WriteLine("1. Download wkhtmltopdf from: https://wkhtmltopdf.org/downloads.html");
		Console.Error.WriteLine("2. Extract wkhtmltox.dll from the archive");
		Console.Error.WriteLine($"3. Copy it to: {Path.GetDirectoryName(wkHtmlToPdfPath)}");
		return false;
	}

	private static void LoadWkHtmlToPdfLibrary(string wkHtmlToPdfPath)
	{
		var context = new CustomAssemblyLoadContext();
		context.LoadUnmanagedLibrary(wkHtmlToPdfPath);
	}

	private static HtmlToPdfDocument CreatePdfDocument((string OutputPath, string AssetsPath, string WkHtmlToPdfPath) paths, string tempDir)
	{
		var mainHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));
		var headerHtmlPath = CreateWrappedHtmlFile(paths.AssetsPath, "header.html", tempDir, includePageNumberScript: false);
		var footerHtmlPath = CreateWrappedHtmlFile(paths.AssetsPath, "footer.html", tempDir, includePageNumberScript: true);

		return new HtmlToPdfDocument
		{
			GlobalSettings = new GlobalSettings
			{
				ColorMode = ColorMode.Color,
				Orientation = Orientation.Portrait,
				PaperSize = PaperKind.Letter,
				Out = paths.OutputPath
			},
			Objects =
			{
				new ObjectSettings
				{
					HtmlContent = mainHtml,
					WebSettings = new WebSettings
					{
						DefaultEncoding = "utf-8",
						EnableJavascript = true
					},
					HeaderSettings = new HeaderSettings
					{
						HtmUrl = headerHtmlPath
					},
					FooterSettings = new FooterSettings
					{
						HtmUrl = footerHtmlPath
					},
					PagesCount = true
				}
			}
		};
	}

	private static HtmlToPdfDocument CreatePdfDocumentWithOutputPath((string OutputPath, string AssetsPath, string WkHtmlToPdfPath) paths, string tempDir, string outputPath)
	{
		var mainHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));
		var headerHtmlPath = CreateWrappedHtmlFile(paths.AssetsPath, "header.html", tempDir, includePageNumberScript: false);
		var footerHtmlPath = CreateWrappedHtmlFile(paths.AssetsPath, "footer.html", tempDir, includePageNumberScript: true);

		return new HtmlToPdfDocument
		{
			GlobalSettings = new GlobalSettings
			{
				ColorMode = ColorMode.Color,
				Orientation = Orientation.Portrait,
				PaperSize = PaperKind.Letter,
				Out = outputPath
			},
			Objects =
			{
				new ObjectSettings
				{
					HtmlContent = mainHtml,
					WebSettings = new WebSettings
					{
						DefaultEncoding = "utf-8",
						EnableJavascript = true
					},
					HeaderSettings = new HeaderSettings
					{
						HtmUrl = headerHtmlPath
					},
					FooterSettings = new FooterSettings
					{
						HtmUrl = footerHtmlPath
					},
					PagesCount = true
				}
			}
		};
	}

	private static string CreateWrappedHtmlFile(string assetsPath, string fileName, string tempDir, bool includePageNumberScript)
	{
		var content = File.ReadAllText(Path.Combine(assetsPath, fileName));
		var wrappedContent = WrapHtmlContent(content, includePageNumberScript);
		var outputPath = Path.Combine(tempDir, fileName);
		File.WriteAllText(outputPath, wrappedContent, Encoding.UTF8);
		return outputPath;
	}

	private static string WrapHtmlContent(string content, bool includePageNumberScript)
	{
		var scriptSection = includePageNumberScript ? @"
<script>
function subst() {
  var vars = {};
  var query_strings_from_url = document.location.search.substring(1).split('&');
  for (var i = 0; i < query_strings_from_url.length; i++) {
    var param = query_strings_from_url[i].split('=', 2);
    if (param.length == 1)
      vars[param[0]] = '';
    else
      vars[param[0]] = decodeURIComponent(param[1].replace(/\+/g, ' '));
  }
  var pageNumEls = document.getElementsByClassName('pageNumber');
  var totalPagesEls = document.getElementsByClassName('totalPages');
  for (var i = 0; i < pageNumEls.length; ++i) pageNumEls[i].textContent = vars.page;
  for (var i = 0; i < totalPagesEls.length; ++i) totalPagesEls[i].textContent = vars.topage;
}
</script>" : "";

		var bodyAttribute = includePageNumberScript ? " onload='subst()'" : "";

		return $@"<!DOCTYPE html>
<html><head>
<meta charset='utf-8'>{scriptSection}
</head><body{bodyAttribute}>
{content}
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
