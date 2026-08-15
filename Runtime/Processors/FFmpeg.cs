using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;

namespace Nox.VideoPlayer.Runtime.Processors {
	public static class FFmpeg {
		public static string GetFolder()
			=> Path.Combine(Constants.ConfigPath, "ffmpeg");

		public static string GetExecutable()
			=> PlatformExtensions.RuntimePlatform switch {
				Platform.Windows => "ffmpeg.exe",
				Platform.Linux   => "ffmpeg",
				Platform.MacOS   => "ffmpeg",
				_                => null
			};

		public static string GetPath()
			=> Path.Combine(GetFolder(), GetExecutable());

		public static CancellationTokenSource DownloadTokenSource;

		public static bool IsDownloading
			=> DownloadTokenSource != null;

		public static void CancelDownload() {
			DownloadTokenSource?.Cancel();
			DownloadTokenSource = null;
		}

		public static async UniTask Download() {
			// https://github.com/BtbN/FFmpeg-Builds/releases

			if (IsDownloading)
				throw new InvalidOperationException("Download already in progress");

			var executable = GetExecutable();
			if (string.IsNullOrEmpty(executable))
				throw new PlatformNotSupportedException("Platform not supported for ffmpeg download");

			var targetPath = GetPath();

			// Check if already exists
			if (File.Exists(targetPath)) {
				Executable.MakeExecutable(targetPath); // ensure permissions even if file pre-exists
				return; // Already downloaded
			}

			DownloadTokenSource = new CancellationTokenSource();
			var cancellationToken = DownloadTokenSource.Token;

			try {
				// Create directory if it doesn't exist
				var folder = GetFolder();
				if (!Directory.Exists(folder))
					Directory.CreateDirectory(folder);

				// Download and extract based on platform
				await DownloadForPlatform(folder, cancellationToken);
			} catch (OperationCanceledException) {
				// Clean up partial download
				if (File.Exists(targetPath))
					File.Delete(targetPath);
				throw;
			} catch (Exception) {
				// Clean up partial download on any error
				if (File.Exists(targetPath))
					File.Delete(targetPath);
				throw;
			} finally {
				DownloadTokenSource = null;
			}
		}

		private static async UniTask DownloadForPlatform(string folder, CancellationToken cancellationToken) {
			string downloadUrl;

			// Get download URL (dynamically for macOS, static for others)
			if (PlatformExtensions.RuntimePlatform == Platform.MacOS) {
				downloadUrl = await GetMacOSDownloadUrl(cancellationToken);
			} else {
				downloadUrl = GetDownloadUrl();
				if (string.IsNullOrEmpty(downloadUrl)) {
					throw new PlatformNotSupportedException("Platform not supported for ffmpeg download");
				}
			}

			var isZip    = downloadUrl.EndsWith(".zip");
			var tempFile = Path.Combine(folder, isZip ? "ffmpeg.zip" : "ffmpeg.tar.xz");

			try {
				using var httpClient = new HttpClient();
				httpClient.Timeout = TimeSpan.FromMinutes(30); // FFmpeg is larger than FFmpeg

				// Download the archive
				using var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
				response.EnsureSuccessStatusCode();

				// Download to temp file with proper stream disposal
				{
					await using var fileStream    = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
					await using var contentStream = await response.Content.ReadAsStreamAsync();
					await contentStream.CopyToAsync(fileStream, cancellationToken);
				} // Ensure streams are disposed before extraction

				// Small delay to ensure file handles are released
				await UniTask.Delay(100, cancellationToken: cancellationToken);

				// Extract the executable
				if (isZip)
					ExtractFromZip(tempFile, folder, cancellationToken);
				else await ExtractFromTarXz(tempFile, folder, cancellationToken);
			} finally {
				// Clean up temp file
				if (File.Exists(tempFile))
					try {
						File.Delete(tempFile);
					} catch {
						// Ignore deletion errors
					}
			}
		}

		private static string GetDownloadUrl() {
			return PlatformExtensions.RuntimePlatform switch {
				Platform.Windows => "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
				Platform.Linux   => "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz",
				Platform.MacOS   => null, // Will be determined dynamically via API
				_                => throw new PlatformNotSupportedException("Platform not supported")
			};
		}

		public class FfMpegDownloadInfo {
			public DownloadInfo download { get; set; }

			public class DownloadInfo {
				public FileInfo zip { get; set; }

				public class FileInfo {
					public string url { get; set; }
				}
			}
		}

		private static async UniTask<string> GetMacOSDownloadUrl(CancellationToken cancellationToken) {
			try {
				using var httpClient = new HttpClient();
				httpClient.Timeout = TimeSpan.FromMinutes(1);

				var response = await httpClient.GetAsync("https://evermeet.cx/ffmpeg/info/ffmpeg", cancellationToken);
				response.EnsureSuccessStatusCode();

				var jsonContent = await response.Content.ReadAsStringAsync();
				var apiResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<FfMpegDownloadInfo>(jsonContent);

				// Extract ZIP download URL from the API response
				var downloadUrl = apiResponse.download.zip.url;

				if (string.IsNullOrEmpty(downloadUrl))
					throw new InvalidOperationException("Unable to get FFmpeg download URL from evermeet.cx API");

				return downloadUrl;
			} catch (Exception ex) {
				throw new InvalidOperationException($"Failed to get FFmpeg download URL for macOS: {ex.Message}", ex);
			}
		}

		private static void ExtractFromZip(string zipFile, string folder, CancellationToken cancellationToken) {
			// For Windows and macOS, we'll use System.IO.Compression
			using var archive = ZipFile.OpenRead(zipFile);

			var executableName = PlatformExtensions.RuntimePlatform == Platform.Windows ? "ffmpeg.exe" : "ffmpeg";

			foreach (var entry in archive.Entries) {
				if (entry.Name.Equals(executableName, StringComparison.OrdinalIgnoreCase)) {
					var       targetPath  = Path.Combine(folder, executableName);
					using var entryStream = entry.Open();
					using var fileStream  = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
					entryStream.CopyTo(fileStream);

					// Make executable on Unix platforms
					Executable.MakeExecutable(targetPath);

					break;
				}
			}
		}

		private static async UniTask ExtractFromTarXz(string tarFile, string folder, CancellationToken cancellationToken) {
			// For Unix platforms, we'll call tar command
			var targetPath = Path.Combine(folder, "ffmpeg");

			var startInfo = new ProcessStartInfo {
				FileName               = "tar",
				Arguments              = $"-xf \"{tarFile}\" --strip-components=2 -C \"{folder}\" --wildcards \"*/bin/ffmpeg\"",
				UseShellExecute        = false,
				RedirectStandardOutput = true,
				RedirectStandardError  = true,
				CreateNoWindow         = true
			};

			using var process = Process.Start(startInfo);
			if (process == null)
				throw new InvalidOperationException("Failed to start tar process");

			while (!process.HasExited) {
				cancellationToken.ThrowIfCancellationRequested();
				await UniTask.Delay(100, cancellationToken: cancellationToken);
			}

			if (process.ExitCode != 0) {
				var error = await process.StandardError.ReadToEndAsync();
				throw new InvalidOperationException($"Failed to extract ffmpeg: {error}");
			}

			// Make executable on Unix platforms
			Executable.MakeExecutable(targetPath);
		}

		public static bool IsAvailable
			=> !IsDownloading && !string.IsNullOrEmpty(GetExecutable()) && File.Exists(GetPath());

		public static async UniTask<bool> WaitReady(int timeout = 60000, CancellationToken cancellationToken = default) {
			if (IsAvailable)
				return true;

			if (!IsDownloading)
				return IsAvailable;

			var sw = Stopwatch.StartNew();
			while (IsDownloading) {
				if (sw.ElapsedMilliseconds > timeout)
					return false;
				await UniTask.Delay(100, cancellationToken: cancellationToken);
			}

			sw.Stop();

			return IsAvailable;
		}

		public static async UniTask<string> GetVersion(CancellationToken cancellationToken = default) {
			var path = GetPath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return null;

			try {
				var startInfo = new ProcessStartInfo {
					FileName               = path,
					Arguments              = "-version",
					UseShellExecute        = false,
					RedirectStandardOutput = true,
					RedirectStandardError  = true,
					CreateNoWindow         = true
				};

				using var process = Process.Start(startInfo);
				if (process == null)
					return null;

				while (!process.HasExited) {
					cancellationToken.ThrowIfCancellationRequested();
					await UniTask.Delay(50, cancellationToken: cancellationToken);
				}

				var output = await process.StandardOutput.ReadToEndAsync();

				if (string.IsNullOrEmpty(output))
					return null;

				var lines = output.Split('\n');
				return (from line in lines
					where line.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase)
					select line.Split(' ') into parts
					where parts.Length >= 3
					select parts[2].Trim())
					.FirstOrDefault();
			} catch (Exception ex) {
				Logger.LogError($"Error getting FFmpeg version: {ex.Message}");
				return null;
			}
		}

		public static async UniTask<string> RunCommand(string arguments, CancellationToken cancellationToken = default) {
			if (!await WaitReady(cancellationToken: cancellationToken))
				throw new InvalidOperationException("FFmpeg not available");

			var path = GetPath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				throw new FileNotFoundException("ffmpeg executable not found", path);

			var     output  = new StringBuilder();
			var     error   = new StringBuilder();
			Process process = null;

			try {
				var startInfo = new ProcessStartInfo {
					FileName               = path,
					Arguments              = arguments,
					UseShellExecute        = false,
					RedirectStandardOutput = true,
					RedirectStandardError  = true,
					CreateNoWindow         = true
				};

				Logger.LogDebug($"{startInfo.FileName} {startInfo.Arguments}");
				process = Process.Start(startInfo);
				if (process == null)
					throw new InvalidOperationException("Failed to start FFmpeg process");

				// Read output and error streams asynchronously
				UniTask.RunOnThreadPool(
						async () => {
							while (!process.HasExited) {
								cancellationToken.ThrowIfCancellationRequested();
								var line = await process.StandardOutput.ReadLineAsync();
								if (line != null)
									output.AppendLine(line);
								else await UniTask.Delay(10, cancellationToken: cancellationToken);
							}

							// Read remaining output after process exits
							while (!process.StandardOutput.EndOfStream) {
								var line = await process.StandardOutput.ReadLineAsync();
								if (line != null)
									output.AppendLine(line);
							}
						}, cancellationToken: cancellationToken
					)
					.Forget();

				UniTask.RunOnThreadPool(
						async () => {
							while (!process.HasExited) {
								cancellationToken.ThrowIfCancellationRequested();
								var line = await process.StandardError.ReadLineAsync();
								if (line != null)
									error.AppendLine(line);
								else await UniTask.Delay(10, cancellationToken: cancellationToken);
							}

							// Read remaining error after process exits
							while (!process.StandardError.EndOfStream) {
								var line = await process.StandardError.ReadLineAsync();
								if (line != null)
									error.AppendLine(line);
							}
						}, cancellationToken: cancellationToken
					)
					.Forget();

				// Wait for process to exit and for output/error reading to complete
				while (!process.HasExited) {
					if (cancellationToken.IsCancellationRequested) {
						try {
							process.Kill();
						} catch {
							// ignored
						}
					}

					await UniTask.Delay(50, cancellationToken: cancellationToken);
				}

				// Check exit code first - FFmpeg returns 0 on success
				var exitCode = process.ExitCode;
				if (exitCode != 0) {
					var errorStr = error.ToString();
					throw new InvalidOperationException($"FFmpeg process failed with exit code {exitCode}. Error: {errorStr}");
				}

				// For successful execution, stderr might contain progress info which is normal
				Logger.LogDebug($"FFmpeg completed successfully with exit code {exitCode}");
			} catch (Exception ex) {
				// Kill process if still running
				try {
					process?.Kill();
				} catch {
					// ignored
				}

				throw new InvalidOperationException($"FFmpeg execution failed: {ex.Message}", ex);
			}

			// Return any output, or empty string if no output (which is normal for some FFmpeg operations)
			var outputStr = output.ToString();

			return outputStr;
		}

		public static UniTask Update() {
			return UniTask.CompletedTask;
		}
	}
}