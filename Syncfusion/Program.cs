using System;
using System.IO;
using System.Linq;
using Syncfusion.HtmlConverter;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;


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

			var bodyHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));
			var headerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "header.html"));
			var footerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "footer.html"));

			HtmlToPdfConverter bodyConverter = new HtmlToPdfConverter();
			BlinkConverterSettings blinkConverterSettings = new BlinkConverterSettings();
			blinkConverterSettings.PdfPageSize = new Syncfusion.Drawing.SizeF(612, 792);
			blinkConverterSettings.Orientation = PdfPageOrientation.Portrait;
			blinkConverterSettings.Margin.All = 0;
			blinkConverterSettings.ViewPortSize = new Syncfusion.Drawing.Size(510, 660);
			bodyConverter.ConverterSettings = blinkConverterSettings;

			PdfDocument bodyDocument = bodyConverter.Convert(bodyHtml, string.Empty);

			string tempBodyPath = Path.Combine(Path.GetTempPath(), $"syncfusion-temp-body-{Guid.NewGuid()}.pdf");
			try
			{
				using (FileStream tempStream = new FileStream(tempBodyPath, FileMode.Create, FileAccess.Write))
				{
					bodyDocument.Save(tempStream);
				}
				bodyDocument.Close(true);

				HtmlToPdfConverter headerConverter = new HtmlToPdfConverter();
				BlinkConverterSettings headerSettings = new BlinkConverterSettings();
				headerSettings.PdfPageSize = new Syncfusion.Drawing.Size(612, 792);
				headerSettings.Margin.All = 0;
				headerSettings.ViewPortSize = new Syncfusion.Drawing.Size(510, 660);
				headerConverter.ConverterSettings = headerSettings;
				PdfDocument headerDocument = headerConverter.Convert(headerHtml, string.Empty);

				HtmlToPdfConverter footerConverter = new HtmlToPdfConverter();
				BlinkConverterSettings footerSettings = new BlinkConverterSettings();
				footerSettings.PdfPageSize = new Syncfusion.Drawing.Size(612, 792);
				footerSettings.Margin.All = 0;
				footerSettings.ViewPortSize = new Syncfusion.Drawing.Size(510, 660);
				footerConverter.ConverterSettings = footerSettings;
				PdfDocument footerDocument = footerConverter.Convert(footerHtml, string.Empty);

				using (FileStream inputStream = new FileStream(tempBodyPath, FileMode.Open, FileAccess.Read))
				{
					using (PdfLoadedDocument loadedDocument = new PdfLoadedDocument(inputStream))
					{
						using (PdfDocument finalDocument = new PdfDocument())
						{
							const float HEADER_HEIGHT = 130f;
							const float FOOTER_HEIGHT = 90f;
							const float PAGE_WIDTH = 612f;
							const float PAGE_HEIGHT = 792f;

							PdfTemplate headerTemplate = headerDocument.Pages[0].CreateTemplate();
							PdfTemplate footerTemplate = footerDocument.Pages[0].CreateTemplate();

							for (int i = 0; i < loadedDocument.Pages.Count; i++)
							{
								PdfPage finalPage = finalDocument.Pages.Add();

								finalPage.Graphics.Save();
								finalPage.Graphics.SetClip(new Syncfusion.Drawing.RectangleF(0, 0, PAGE_WIDTH, HEADER_HEIGHT));
								finalPage.Graphics.DrawPdfTemplate(headerTemplate, new Syncfusion.Drawing.PointF(0, 0));
								finalPage.Graphics.Restore();

								finalPage.Graphics.Save();
								PdfTemplate bodyTemplate = loadedDocument.Pages[i].CreateTemplate();
								float bodyYPosition = HEADER_HEIGHT;
								float bodyHeight = PAGE_HEIGHT - HEADER_HEIGHT - FOOTER_HEIGHT;
								finalPage.Graphics.SetClip(new Syncfusion.Drawing.RectangleF(0, bodyYPosition, PAGE_WIDTH, bodyHeight));
								finalPage.Graphics.DrawPdfTemplate(bodyTemplate, new Syncfusion.Drawing.PointF(0, 0));
								finalPage.Graphics.Restore();

								finalPage.Graphics.Save();
								float footerYPosition = PAGE_HEIGHT - FOOTER_HEIGHT;
								finalPage.Graphics.SetClip(new Syncfusion.Drawing.RectangleF(0, footerYPosition, PAGE_WIDTH, FOOTER_HEIGHT));
								finalPage.Graphics.DrawPdfTemplate(footerTemplate, new Syncfusion.Drawing.PointF(0, footerYPosition));
								finalPage.Graphics.Restore();
							}

							Directory.CreateDirectory(Path.GetDirectoryName(paths.OutputPath)!);
							using (FileStream fileStream = new FileStream(paths.OutputPath, FileMode.Create, FileAccess.Write))
							{
								finalDocument.Save(fileStream);
							}
						}
					}
				}

				headerDocument.Close(true);
				footerDocument.Close(true);

				Console.WriteLine($"PDF saved to: {paths.OutputPath}");

				PrintMemoryUsage(startMemory);

				return 0;
			}
			finally
			{
				if (File.Exists(tempBodyPath))
				{
					try
					{
						File.Delete(tempBodyPath);
					}
					catch
					{
						// Ignore deletion errors - temp files will be cleaned up by OS eventually
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] Syncfusion failed: {ex.GetType().Name}: {ex.Message}");
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

			var bodyHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "main.html"));
			var headerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "header.html"));
			var footerHtml = File.ReadAllText(Path.Combine(paths.AssetsPath, "footer.html"));

			HtmlToPdfConverter bodyConverter = new HtmlToPdfConverter();
			BlinkConverterSettings blinkConverterSettings = new BlinkConverterSettings();
			blinkConverterSettings.PdfPageSize = new Syncfusion.Drawing.SizeF(612, 792);
			blinkConverterSettings.Orientation = PdfPageOrientation.Portrait;
			blinkConverterSettings.Margin.All = 0;
			blinkConverterSettings.ViewPortSize = new Syncfusion.Drawing.Size(510, 660);
			bodyConverter.ConverterSettings = blinkConverterSettings;

			PdfDocument bodyDocument = bodyConverter.Convert(bodyHtml, string.Empty);

			HtmlToPdfConverter headerConverter = new HtmlToPdfConverter();
			BlinkConverterSettings headerSettings = new BlinkConverterSettings();
			headerSettings.PdfPageSize = new Syncfusion.Drawing.Size(612, 792);
			headerSettings.Margin.All = 0;
			headerSettings.ViewPortSize = new Syncfusion.Drawing.Size(510, 660);
			headerConverter.ConverterSettings = headerSettings;
			PdfDocument headerDocument = headerConverter.Convert(headerHtml, string.Empty);

			HtmlToPdfConverter footerConverter = new HtmlToPdfConverter();
			BlinkConverterSettings footerSettings = new BlinkConverterSettings();
			footerSettings.PdfPageSize = new Syncfusion.Drawing.Size(612, 792);
			footerSettings.Margin.All = 0;
			footerSettings.ViewPortSize = new Syncfusion.Drawing.Size(510, 660);
			footerConverter.ConverterSettings = footerSettings;
			PdfDocument footerDocument = footerConverter.Convert(footerHtml, string.Empty);

			const float HEADER_HEIGHT = 130f;
			const float FOOTER_HEIGHT = 90f;
			const float PAGE_WIDTH = 612f;
			const float PAGE_HEIGHT = 792f;

			PdfTemplate headerTemplate = headerDocument.Pages[0].CreateTemplate();
			PdfTemplate footerTemplate = footerDocument.Pages[0].CreateTemplate();

			int successCount = 0;
			for (int i = 0; i < outputPaths.Length; i++)
			{
				try
				{
					var outputPath = outputPaths[i];
					Console.WriteLine($"Generating PDF {i + 1}/{outputPaths.Length}: {outputPath}");

					string tempBodyPath = Path.Combine(Path.GetTempPath(), $"syncfusion-temp-body-{Guid.NewGuid()}.pdf");
					try
					{
						using (FileStream tempStream = new FileStream(tempBodyPath, FileMode.Create, FileAccess.Write))
						{
							bodyDocument.Save(tempStream);
						}

						using (FileStream inputStream = new FileStream(tempBodyPath, FileMode.Open, FileAccess.Read))
						{
							using (PdfLoadedDocument loadedDocument = new PdfLoadedDocument(inputStream))
							{
								using (PdfDocument finalDocument = new PdfDocument())
								{
									for (int j = 0; j < loadedDocument.Pages.Count; j++)
									{
										PdfPage finalPage = finalDocument.Pages.Add();

										finalPage.Graphics.Save();
										finalPage.Graphics.SetClip(new Syncfusion.Drawing.RectangleF(0, 0, PAGE_WIDTH, HEADER_HEIGHT));
										finalPage.Graphics.DrawPdfTemplate(headerTemplate, new Syncfusion.Drawing.PointF(0, 0));
										finalPage.Graphics.Restore();

										finalPage.Graphics.Save();
										PdfTemplate bodyTemplate = loadedDocument.Pages[j].CreateTemplate();
										float bodyYPosition = HEADER_HEIGHT;
										float bodyHeight = PAGE_HEIGHT - HEADER_HEIGHT - FOOTER_HEIGHT;
										finalPage.Graphics.SetClip(new Syncfusion.Drawing.RectangleF(0, bodyYPosition, PAGE_WIDTH, bodyHeight));
										finalPage.Graphics.DrawPdfTemplate(bodyTemplate, new Syncfusion.Drawing.PointF(0, 0));
										finalPage.Graphics.Restore();

										finalPage.Graphics.Save();
										float footerYPosition = PAGE_HEIGHT - FOOTER_HEIGHT;
										finalPage.Graphics.SetClip(new Syncfusion.Drawing.RectangleF(0, footerYPosition, PAGE_WIDTH, FOOTER_HEIGHT));
										finalPage.Graphics.DrawPdfTemplate(footerTemplate, new Syncfusion.Drawing.PointF(0, footerYPosition));
										finalPage.Graphics.Restore();
									}

									Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
									using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
									{
										finalDocument.Save(fileStream);
									}
								}
							}
						}

						Console.WriteLine($"PDF saved to: {outputPath}");
						successCount++;
					}
					finally
					{
						if (File.Exists(tempBodyPath))
						{
							try
							{
								File.Delete(tempBodyPath);
							}
							catch
							{
								// Ignore deletion errors - temp files will be cleaned up by OS eventually
							}
						}
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"[ERROR] Failed to generate PDF {i + 1}/{outputPaths.Length}: {ex.GetType().Name}: {ex.Message}");
				}
			}

			bodyDocument.Close(true);
			headerDocument.Close(true);
			footerDocument.Close(true);

			PrintMemoryUsage(startMemory);
			Console.WriteLine($"Sequential generation completed: {successCount}/{outputPaths.Length} PDFs generated successfully.");

			return successCount == outputPaths.Length ? 0 : 1;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] Sequential Syncfusion failed: {ex.GetType().Name}: {ex.Message}");
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
		var outputPath = Path.GetFullPath(outputPathArg ?? Path.Combine(projectDirectory, "output-Syncfusion.pdf"));
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
