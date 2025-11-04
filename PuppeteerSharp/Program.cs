using PuppeteerSharp;
using PuppeteerSharp.Media;

internal class Program
{
	private const int ChromiumDownloadMaxRetries = 5;
	private const int ChromiumDownloadDelayMs = 1000;
	private const int ChromiumLaunchMaxRetries = 10;
	private const int ChromiumLaunchDelayMs = 5000;
	private const int ChromiumLaunchTimeoutSeconds = 30;

	private static async Task<int> Main(string[] args)
	{
		try
		{
			// Check for sequential mode
			if (args.Length > 0 && args[0] == "--sequential")
			{
				return await RunSequentialAsync(args.Skip(1).ToArray());
			}

			// Single PDF mode (existing functionality)
			Console.WriteLine($"Starting PuppeteerSharp PDF generation at {DateTime.Now:O}");

			var startMemory = MeasureCurrentMemory();
			var paths = ResolvePaths(args);

			await EnsureChromiumWithRetryAsync(new BrowserFetcher(), ChromiumDownloadMaxRetries, ChromiumDownloadDelayMs);

			var browser = await LaunchChromiumWithRetryAsync(ChromiumLaunchMaxRetries, ChromiumLaunchDelayMs);

			await using var page = await browser.NewPageAsync();

			await LoadHtmlContentAsync(page, paths.AssetsPath);

			await GeneratePdfAsync(page, paths.OutputPath, paths.AssetsPath);
			Console.WriteLine($"PDF saved to: {paths.OutputPath}");

			await browser.CloseAsync();

			PrintMemoryUsage(startMemory);

			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] PuppeteerSharp failed: {ex.GetType().Name}: {ex.Message}");
			Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
			return 1;
		}
	}

	private static async Task<int> RunSequentialAsync(string[] outputPaths)
	{
		try
		{
			var startMemory = MeasureCurrentMemory();
			var paths = ResolvePaths(new string[0]); // Use default path for assets

			Console.WriteLine($"Starting sequential PDF generation for {outputPaths.Length} PDFs at {DateTime.Now:O}");

			await EnsureChromiumWithRetryAsync(new BrowserFetcher(), ChromiumDownloadMaxRetries, ChromiumDownloadDelayMs);

			var browser = await LaunchChromiumWithRetryAsync(ChromiumLaunchMaxRetries, ChromiumLaunchDelayMs);

			await using var page = await browser.NewPageAsync();
			await LoadHtmlContentAsync(page, paths.AssetsPath);

			int successCount = 0;
			for (int i = 0; i < outputPaths.Length; i++)
			{
				try
				{
					var outputPath = outputPaths[i];
					Console.WriteLine($"Generating PDF {i + 1}/{outputPaths.Length}: {outputPath}");

					await GeneratePdfAsync(page, outputPath, paths.AssetsPath);
					Console.WriteLine($"PDF saved to: {outputPath}");
					successCount++;
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"[ERROR] Failed to generate PDF {i + 1}/{outputPaths.Length}: {ex.GetType().Name}: {ex.Message}");
				}
			}

			await browser.CloseAsync();

			PrintMemoryUsage(startMemory);
			Console.WriteLine($"Sequential generation completed: {successCount}/{outputPaths.Length} PDFs generated successfully.");

			return successCount == outputPaths.Length ? 0 : 1;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[ERROR] Sequential PuppeteerSharp failed: {ex.GetType().Name}: {ex.Message}");
			Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
			return 1;
		}
	}

	private static async Task<IBrowser> LaunchChromiumWithRetryAsync(int maxRetries, int delayMs)
	{
		Exception? lastException = null;

		for (int attempt = 1; attempt <= maxRetries; attempt++)
		{
			try
			{
				var launchOptions = new LaunchOptions
				{
					Headless = true,
					Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" },
					Timeout = ChromiumLaunchTimeoutSeconds * 1000
				};

				var launchTask = Puppeteer.LaunchAsync(launchOptions);
				var timeoutTask = Task.Delay(TimeSpan.FromSeconds(ChromiumLaunchTimeoutSeconds));

				Console.WriteLine($"Waiting for Chromium to launch ({ChromiumLaunchTimeoutSeconds}s timeout)...");
				var completedTask = await Task.WhenAny(launchTask, timeoutTask);

				if (completedTask == timeoutTask)
				{
					Console.Error.WriteLine($"[WARN] Chromium launch timed out after {ChromiumLaunchTimeoutSeconds} seconds");
					throw new TimeoutException($"Chromium launch timed out after {ChromiumLaunchTimeoutSeconds} seconds");
				}

				var browser = await launchTask;
				Console.WriteLine($"Chromium launched successfully (PID: {browser.Process?.Id ?? -1})");
				return browser;
			}
			catch (Exception ex)
			{
				lastException = ex;
				Console.Error.WriteLine($"[WARN] Chromium launch attempt {attempt}/{maxRetries} failed: {ex.GetType().Name}: {ex.Message}");

				if (attempt < maxRetries)
				{
					var waitTime = CalculateRetryDelay(delayMs);
					Console.WriteLine($"Waiting {waitTime}ms before retry...");
					await Task.Delay(waitTime);
				}
			}
		}

		throw new Exception($"Failed to launch Chromium after {maxRetries} attempts. Last error: {lastException?.Message}", lastException);
	}

	private static async Task EnsureChromiumWithRetryAsync(BrowserFetcher browserFetcher, int maxRetries, int delayMs)
	{
		if (browserFetcher.GetInstalledBrowsers().Any())
		{
			Console.WriteLine("Chromium already downloaded, skipping fetch.");
			return;
		}

		for (int attempt = 1; attempt <= maxRetries; attempt++)
		{
			try
			{
				Console.WriteLine($"Attempting to download Chromium (attempt {attempt}/{maxRetries})...");
				await browserFetcher.DownloadAsync();
				Console.WriteLine("Chromium download successful.");
				return;
			}
			catch (Exception ex) when (IsFileLockException(ex))
			{
				if (attempt < maxRetries)
				{
					var waitTime = CalculateRetryDelay(delayMs);
					Console.WriteLine($"Chromium download conflict detected, waiting {waitTime}ms before retry...");
					await Task.Delay(waitTime);
				}
				else if (browserFetcher.GetInstalledBrowsers().Any())
				{
					Console.WriteLine("Chromium downloaded by another process, continuing.");
					return;
				}
				else
				{
					throw;
				}
			}
		}
	}

	private static bool IsFileLockException(Exception ex)
	{
		return (ex is IOException ioEx && ioEx.Message.Contains("being used by another process")) ||
		       (ex is System.Net.WebException webEx &&
		        webEx.InnerException is IOException innerIoEx &&
		        innerIoEx.Message.Contains("being used by another process"));
	}

	private static int CalculateRetryDelay(int baseDelayMs)
	{
		var jitter = Random.Shared.Next(0, baseDelayMs / 2);
		return baseDelayMs + jitter;
	}

	private static (string OutputPath, string AssetsPath) ResolvePaths(string[] args)
	{
		Console.WriteLine($"Resolving paths...");
		var projectDirectory = Directory.GetParent(AppContext.BaseDirectory)!
			.Parent!
			.Parent!
			.Parent!
			.FullName;

		var outputPathArg = args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
		var outputPath = Path.GetFullPath(outputPathArg ?? Path.Combine(projectDirectory, "output-PuppeteerSharp.pdf"));
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

		var assetsPath = Path.Combine(projectDirectory, "..", "assets");
		return (outputPath, assetsPath);
	}

	private static async Task LoadHtmlContentAsync(IPage page, string assetsPath)
	{
		var html = File.ReadAllText(Path.Combine(assetsPath, "main.html"));
		await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } });
	}

	private static async Task GeneratePdfAsync(IPage page, string outputPath, string assetsPath)
	{
		var headerHtml = File.ReadAllText(Path.Combine(assetsPath, "header.html"));
		var footerHtml = File.ReadAllText(Path.Combine(assetsPath, "footer.html"));

		await page.PdfAsync(outputPath, new PdfOptions
		{
			Format = PaperFormat.Letter,
			DisplayHeaderFooter = true,
			HeaderTemplate = headerHtml,
			FooterTemplate = footerHtml,
			PrintBackground = true
		});
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
