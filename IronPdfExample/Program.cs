using System;
using System.IO;
using System.Linq;
using IronPdf;

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

			// Check for license key
			var licenseKey = Environment.GetEnvironmentVariable("IRONPDF_LICENSE_KEY");
			if (string.IsNullOrEmpty(licenseKey))
			{
				Console.WriteLine("[INFO] IronPDF requires a license key to run.");
				Console.WriteLine("[INFO] To use IronPDF:");
				Console.WriteLine("[INFO]   1. Request a trial key at: https://ironpdf.com/licensing/");
				Console.WriteLine("[INFO]   2. Set environment variable: IRONPDF_LICENSE_KEY=your-key-here");
				Console.WriteLine("[INFO] Skipping IronPDF execution.");
				return 0;
			}

			License.LicenseKey = licenseKey;

			// Single PDF mode (existing functionality)
			var startMemory = MeasureCurrentMemory();
			var paths = ResolvePaths(args);

			var headerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "header.html"));
			var footerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "footer.html"));
			var bodyHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));

			// Replace CSS classes with IronPDF page number placeholders
			footerHtml = footerHtml.Replace("<span class=\"pageNumber\"></span>", "{page}")
									.Replace("<span class=\"totalPages\"></span>", "{total-pages}");

			// Increase font sizes for better readability
			headerHtml = headerHtml.Replace("font-size:9px", "font-size:11px")
								 .Replace("font-size:12px", "font-size:14px")
								 .Replace("font-size:24px", "font-size:28px");

			footerHtml = footerHtml.Replace("font-size:9px", "font-size:11px")
								 .Replace("font-size:10px", "font-size:12px");

			var renderer = new ChromePdfRenderer();

			// Configure for US Letter size (8.5 x 11 inches)
			renderer.RenderingOptions.PaperSize = IronPdf.Rendering.PdfPaperSize.Letter;
			renderer.RenderingOptions.MarginLeft = 15;
			renderer.RenderingOptions.MarginRight = 15;
			renderer.RenderingOptions.MarginBottom = 0;
			renderer.RenderingOptions.MarginTop = 0;
			renderer.RenderingOptions.UseMarginsOnHeaderAndFooter = UseMargins.None;

			// Set HTML header and footer
			renderer.RenderingOptions.HtmlHeader = new HtmlHeaderFooter()
			{
				HtmlFragment = headerHtml,
			};
			
			renderer.RenderingOptions.HtmlFooter = new HtmlHeaderFooter()
			{
				HtmlFragment = footerHtml,
			};

			var pdf = renderer.RenderHtmlAsPdf(bodyHtml);

			Directory.CreateDirectory(Path.GetDirectoryName(paths.OutputPath)!);
			pdf.SaveAs(paths.OutputPath);
			Console.WriteLine($"PDF saved to: {paths.OutputPath}");

			PrintMemoryUsage(startMemory);

			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] IronPdfExample failed: {ex.GetType().Name}: {ex.Message}");
			Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
			return 1;
		}
	}

	static int RunSequential(string[] outputPaths)
	{
		try
		{
			// Check for license key
			//var licenseKey = Environment.GetEnvironmentVariable("IRONPDF_LICENSE_KEY");
			var licenseKey = "IRONSUITE.JAKUB.KACZAROWSKI.CUSTOMIZEIT.PL.8670-F03D26E329-C5SRYSEF6YOKC4-II7FFE3BJYO4-MHL4LZFAIQDE-XNEDMSQJSXWF-LVMU6TJSF4UU-W777JKT5RYTZ-DBQQ5D-TVC2FK3M25SQEA-DEPLOYMENT.TRIAL-VIUCVF.TRIAL.EXPIRES.06.DEC.2025";
			if (string.IsNullOrEmpty(licenseKey))
			{
				Console.WriteLine("[INFO] IronPDF requires a license key to run.");
				Console.WriteLine("[INFO] Skipping IronPDF sequential execution.");
				return 0;
			}

			License.LicenseKey = licenseKey;

			var startMemory = MeasureCurrentMemory();
			var paths = ResolvePaths(new string[0]); // Use default path for assets

			Console.WriteLine($"Starting sequential PDF generation for {outputPaths.Length} PDFs at {DateTime.Now:O}");

			var headerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "header.html"));
			var footerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "footer.html"));
			var bodyHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));

			// Replace CSS classes with IronPDF page number placeholders
			footerHtml = footerHtml.Replace("<span class=\"pageNumber\"></span>", "{page}")
									.Replace("<span class=\"totalPages\"></span>", "{total-pages}");

			// Increase font sizes for better readability
			headerHtml = headerHtml.Replace("font-size:9px", "font-size:11px")
								 .Replace("font-size:12px", "font-size:14px")
								 .Replace("font-size:24px", "font-size:28px");

			footerHtml = footerHtml.Replace("font-size:9px", "font-size:11px")
								 .Replace("font-size:10px", "font-size:12px");

			var renderer = new ChromePdfRenderer();

			// Configure for US Letter size (8.5 x 11 inches)
			renderer.RenderingOptions.PaperSize = IronPdf.Rendering.PdfPaperSize.Letter;
			renderer.RenderingOptions.MarginLeft = 15;
			renderer.RenderingOptions.MarginRight = 15;
			renderer.RenderingOptions.MarginBottom = 0;
			renderer.RenderingOptions.MarginTop = 0;
			renderer.RenderingOptions.UseMarginsOnHeaderAndFooter = UseMargins.None;

			// Set HTML header and footer
			renderer.RenderingOptions.HtmlHeader = new HtmlHeaderFooter()
			{
				HtmlFragment = headerHtml,
			};
			
			renderer.RenderingOptions.HtmlFooter = new HtmlHeaderFooter()
			{
				HtmlFragment = footerHtml,
			};

			int successCount = 0;
			for (int i = 0; i < outputPaths.Length; i++)
			{
				try
				{
					var outputPath = outputPaths[i];
					Console.WriteLine($"Generating PDF {i + 1}/{outputPaths.Length}: {outputPath}");

					var pdf = renderer.RenderHtmlAsPdf(bodyHtml);

					Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
					pdf.SaveAs(outputPath);
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
			Console.Error.WriteLine($"[ERROR] Sequential IronPdfExample failed: {ex.GetType().Name}: {ex.Message}");
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
		var outputPath = Path.GetFullPath(outputPathArg ?? Path.Combine(projectDirectory, "output-IronPdfExample.pdf"));
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
