using System.Linq;
using System.Text.RegularExpressions;
using SelectPdf;

class Program
{
	private const int HeaderHeight = 110;
	private const int FooterHeight = 72;
	private const int PageNumberFontSize = 12;

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

			var mainHtml = AdaptHtmlForSelectPdf(File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html")));
			var converter = CreateHtmlToPdfConverter(paths.AssetsPath);

			var baseUrl = new Uri(Path.GetFullPath(paths.AssetsPath)).AbsoluteUri + "/";
			var doc = converter.ConvertHtmlString(mainHtml, baseUrl);

			Directory.CreateDirectory(Path.GetDirectoryName(paths.OutputPath)!);
			doc.Save(paths.OutputPath);
			doc.Close();

			Console.WriteLine($"PDF saved to: {paths.OutputPath}");
			PrintMemoryUsage(startMemory);

			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] SelectPdf failed: {ex.GetType().Name}: {ex.Message}");
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

			var mainHtml = AdaptHtmlForSelectPdf(File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html")));
			var converter = CreateHtmlToPdfConverter(paths.AssetsPath);

			var baseUrl = new Uri(Path.GetFullPath(paths.AssetsPath)).AbsoluteUri + "/";

			int successCount = 0;
			for (int i = 0; i < outputPaths.Length; i++)
			{
				try
				{
					var outputPath = outputPaths[i];
					Console.WriteLine($"Generating PDF {i + 1}/{outputPaths.Length}: {outputPath}");

					var doc = converter.ConvertHtmlString(mainHtml, baseUrl);

					Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
					doc.Save(outputPath);
					doc.Close();

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
			Console.Error.WriteLine($"[ERROR] Sequential SelectPdf failed: {ex.GetType().Name}: {ex.Message}");
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
		var outputPath = Path.GetFullPath(outputPathArg ?? Path.Combine(projectDirectory, "output-SelectPdf.pdf"));
		var assetsPath = Path.Combine(projectDirectory, "..", "assets");

		return (outputPath, assetsPath);
	}

	private static HtmlToPdf CreateHtmlToPdfConverter(string assetsPath)
	{
		var converter = new HtmlToPdf();

		ConfigurePageSettings(converter.Options);
		ConfigureHeader(converter, assetsPath);
		ConfigureFooter(converter, assetsPath);

		return converter;
	}

	private static void ConfigurePageSettings(HtmlToPdfOptions options)
	{
		options.PdfPageSize = PdfPageSize.Letter;
		options.PdfPageOrientation = PdfPageOrientation.Portrait;
		options.MarginTop = 50;
		options.MarginBottom = 50;
		options.MarginLeft = 15;
		options.MarginRight = 15;
		options.WebPageWidth = 780;
		options.WebPageHeight = 0;
		options.AutoFitWidth = HtmlToPdfPageFitMode.AutoFit;
		options.CssMediaType = HtmlToPdfCssMediaType.Print;
		options.JavaScriptEnabled = true;

		try
		{
			options.RenderingEngine = RenderingEngine.WebKit;
		}
		catch
		{
		}
	}

	private static void ConfigureHeader(HtmlToPdf converter, string assetsPath)
	{
		converter.Options.DisplayHeader = true;
		converter.Header.DisplayOnFirstPage = true;
		converter.Header.DisplayOnOddPages = true;
		converter.Header.DisplayOnEvenPages = true;
		converter.Header.Height = HeaderHeight;

		var headerHtml = AdaptHtmlForSelectPdf(File.ReadAllText(Path.Combine(assetsPath, "header.html")));
		var headerHtmlWrapped = WrapInHtmlDocument(headerHtml);
		var headerSection = new PdfHtmlSection(headerHtmlWrapped, string.Empty)
		{
			AutoFitHeight = HtmlToPdfPageFitMode.AutoFit
		};
		converter.Header.Add(headerSection);
	}

	private static void ConfigureFooter(HtmlToPdf converter, string assetsPath)
	{
		converter.Options.DisplayFooter = true;
		converter.Footer.DisplayOnFirstPage = true;
		converter.Footer.DisplayOnOddPages = true;
		converter.Footer.DisplayOnEvenPages = true;
		converter.Footer.Height = FooterHeight;

		var footerHtml = AdaptHtmlForSelectPdf(File.ReadAllText(Path.Combine(assetsPath, "footer.html")));
		var footerHtmlWrapped = WrapInHtmlDocument(footerHtml);
		var footerSection = new PdfHtmlSection(footerHtmlWrapped, string.Empty)
		{
			AutoFitHeight = HtmlToPdfPageFitMode.AutoFit
		};
		converter.Footer.Add(footerSection);

		AddPageNumberOverlay(converter);
	}

	private static void AddPageNumberOverlay(HtmlToPdf converter)
	{
		var pageNumberText = "[ Page {page_number} / {total_pages} ]";
		var font = new System.Drawing.Font("Arial", PageNumberFontSize, System.Drawing.FontStyle.Bold);

		var outlineTop = new PdfTextSection(0, 7, pageNumberText, font)
		{
			HorizontalAlign = PdfTextHorizontalAlign.Right,
			ForeColor = System.Drawing.Color.White
		};
		converter.Footer.Add(outlineTop);

		var outlineBottom = new PdfTextSection(0, 9, pageNumberText, font)
		{
			HorizontalAlign = PdfTextHorizontalAlign.Right,
			ForeColor = System.Drawing.Color.White
		};
		converter.Footer.Add(outlineBottom);

		var mainText = new PdfTextSection(0, 8, pageNumberText, font)
		{
			HorizontalAlign = PdfTextHorizontalAlign.Right,
			ForeColor = System.Drawing.Color.FromArgb(17, 24, 39)
		};
		converter.Footer.Add(mainText);
	}

	private static string WrapInHtmlDocument(string content)
	{
		return $"<html><head><meta charset='utf-8'></head><body>{content}</body></html>";
	}

	private static string AdaptHtmlForSelectPdf(string html)
	{
		html = Regex.Replace(html, @":root\s*\{[^}]+\}", "", RegexOptions.Singleline);
		html = ReplaceCssVariables(html);
		html = Regex.Replace(html, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
		html = html.Replace("margin: 250px 40px 140px 40px", "margin: 0");
		return html;
	}

	private static string ReplaceCssVariables(string html)
	{
		var cssVariables = new Dictionary<string, string>
		{
			{ "var(--ink)", "#0f172a" },
			{ "var(--muted)", "#475569" },
			{ "var(--border)", "#e2e8f0" },
			{ "var(--border-subtle)", "#eef2f7" },
			{ "var(--header-bg1)", "#f8fafc" },
			{ "var(--header-bg2)", "#f1f5f9" },
			{ "var(--shadow)", "rgba(15,23,42,.06)" },
			{ "var(--primary)", "#0ea5e9" },
			{ "var(--success)", "#059669" },
			{ "var(--danger)", "#dc2626" },
			{ "var(--warn)", "#f59e0b" },
			{ "var(--warn-bg)", "#fff7ed" },
			{ "var(--warn-border)", "#fed7aa" },
			{ "var(--info)", "#1d4ed8" }
		};

		foreach (var kvp in cssVariables)
		{
			html = html.Replace(kvp.Key, kvp.Value);
		}

		return html;
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
