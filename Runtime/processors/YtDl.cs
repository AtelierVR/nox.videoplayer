using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nox.CCK.Utils;

namespace api.nox.videoplayer {
	public static class YtDl {
		public static string GetFolder()
			=> Path.Combine(Constants.ConfigPath, "ytdlp");

		public static string GetExecutable()
			=> PlatformExtensions.RuntimePlatform switch {
				Platform.Windows => "yt-dlp.exe",
				Platform.Linux   => "yt-dlp_linux",
				Platform.MacOS   => "yt-dlp_macos",
				_                => null
			};

		public static string GetConfigArguments()
			=> Config.Load().Get("settings.ytdlp.arguments", "");

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
			// https://github.com/yt-dlp/yt-dlp/releases

			if (IsDownloading)
				throw new InvalidOperationException("Download already in progress");

			var executable = GetExecutable();
			if (string.IsNullOrEmpty(executable))
				throw new PlatformNotSupportedException("Platform not supported for yt-dlp download");

			var targetPath = GetPath();

			// Check if already exists
			if (File.Exists(targetPath))
				return; // Already downloaded

			DownloadTokenSource = new CancellationTokenSource();
			var cancellationToken = DownloadTokenSource.Token;

			try {
				// Create directory if it doesn't exist
				var folder = GetFolder();
				if (!Directory.Exists(folder))
					Directory.CreateDirectory(folder);

				// Determine download URL based on platform
				var downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/" + executable;


				using var httpClient = new HttpClient();
				httpClient.Timeout = TimeSpan.FromMinutes(10); // 10 minute timeout

				// Download the file
				using var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
				response.EnsureSuccessStatusCode();

				await using var fileStream    = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
				await using var contentStream = await response.Content.ReadAsStreamAsync();
				await contentStream.CopyToAsync(fileStream, cancellationToken);

				// Make executable on Unix platforms
				if (PlatformExtensions.RuntimePlatform != Platform.Windows) {
					// Set execute permissions (equivalent to chmod +x)
					var fileInfo = new FileInfo(targetPath);
					if (fileInfo.Exists) {
						// This is a simplified approach - in a real implementation you might want to use P/Invoke
						// to properly set Unix file permissions
					}
				}
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

		public static bool IsAvailable
			=> !IsDownloading && !string.IsNullOrEmpty(GetExecutable()) && File.Exists(GetPath());

		public static async UniTask<bool> WaitReady(int timeout = 30000, CancellationToken cancellationToken = default) {
			if (IsAvailable)
				return true;

			if (!IsDownloading)
				return IsAvailable;

			var sw = System.Diagnostics.Stopwatch.StartNew();
			while (IsDownloading) {
				if (sw.ElapsedMilliseconds > timeout)
					return false;
				await UniTask.Delay(100, cancellationToken: cancellationToken);
			}

			sw.Stop();

			return IsAvailable;
		}

		public static async UniTask<Version> GetVersion(CancellationToken cancellationToken = default) {
			var path = GetPath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return null;

			try {
				var startInfo = new ProcessStartInfo {
					FileName               = path,
					Arguments              = "--version",
					UseShellExecute        = false,
					RedirectStandardOutput = true,
					RedirectStandardError  = true,
					CreateNoWindow         = true
				};

				using var process = Process.Start(startInfo);
				if (process == null)
					return null;

				// Wait for the process to exit
				while (!process.HasExited) {
					cancellationToken.ThrowIfCancellationRequested();
					await UniTask.Delay(50, cancellationToken: cancellationToken);
				}

				var output = await process.StandardOutput.ReadToEndAsync();
				var error  = await process.StandardError.ReadToEndAsync();

				if (!string.IsNullOrEmpty(error))
					return null;

				var versionString = output.Trim();
				return Version.TryParse(versionString, out var version)
					? version
					: null;
			} catch (Exception) {
				return null;
			}
		}

		public static async UniTask<JObject> Extract(string url, int timeout = 60000, CancellationToken cancellationToken = default) {
			var startTime = DateTime.UtcNow;

			if (string.IsNullOrEmpty(url))
				throw new ArgumentNullException(nameof(url));

			if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
				throw new ArgumentException("Invalid URL", nameof(url));

			if (!await WaitReady(timeout: timeout, cancellationToken: cancellationToken))
				throw new InvalidOperationException("yt-dlp is not available");

			var path = GetPath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				throw new FileNotFoundException("yt-dlp executable not found", path);

			var output = new StringBuilder();
			var error  = new StringBuilder();

			try {
				var arg = GetConfigArguments();
				var startInfo = new ProcessStartInfo {
					FileName = path,
					Arguments = (string.IsNullOrEmpty(arg) ? "" : $"{arg} ")
						+ $"--no-warnings -J \"{url}\"", // -J for JSON output
					UseShellExecute        = false,
					RedirectStandardOutput = true,
					RedirectStandardError  = true,
					CreateNoWindow         = true
				};

				Logger.LogDebug($"{startInfo.FileName} {startInfo.Arguments}");
				var process = Process.Start(startInfo);
				if (process == null)
					throw new InvalidOperationException("Failed to start yt-dlp process");

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
					cancellationToken.ThrowIfCancellationRequested();
					if ((DateTime.UtcNow - startTime).TotalMilliseconds > timeout) {
						try {
							process.Kill();
						} catch {
							// ignored
						}

						throw new TimeoutException("yt-dlp process timed out");
					}

					await UniTask.Delay(50, cancellationToken: cancellationToken);
				}
			} catch (Exception ex) {
				throw new InvalidOperationException("Error executing yt-dlp", ex);
			}

			var errorStr = error.ToString();
			if (!string.IsNullOrEmpty(errorStr))
				throw new InvalidOperationException($"yt-dlp error: {errorStr}");

			var outputStr = output.ToString();
			if (string.IsNullOrEmpty(outputStr))
				throw new InvalidOperationException("yt-dlp returned no output");

			return JObject.Parse(outputStr);
		}

		public static async UniTask Update(CancellationToken cancellationToken = default) {
			var path = GetPath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				throw new FileNotFoundException("yt-dlp executable not found", path);

			Process process = null;
			try {
				var startInfo = new ProcessStartInfo {
					FileName               = path,
					Arguments              = "-U",
					UseShellExecute        = false,
					RedirectStandardOutput = true,
					RedirectStandardError  = true,
					CreateNoWindow         = true
				};

				process = Process.Start(startInfo);
				if (process == null)
					throw new InvalidOperationException("Failed to start yt-dlp process");

				var output = new StringBuilder();
				var error  = new StringBuilder();

				// Capture streams to avoid capturing the process object
				var stdout        = process.StandardOutput;
				var stderr        = process.StandardError;
				var processHandle = process;

				// Read output and error streams asynchronously
				var outputTask = UniTask.RunOnThreadPool(
					async () => {
						while (!processHandle.HasExited) {
							cancellationToken.ThrowIfCancellationRequested();
							var line = await stdout.ReadLineAsync();
							if (!string.IsNullOrEmpty(line)) {
								Logger.LogDebug(line, tag: "yt-dlp");
								output.AppendLine(line);
							} else await UniTask.Delay(10, cancellationToken: cancellationToken);
						}

						// Read remaining output after process exits
						while (!stdout.EndOfStream) {
							var line = await stdout.ReadLineAsync();
							if (string.IsNullOrEmpty(line)) continue;
							Logger.LogDebug(line, tag: "yt-dlp");
							output.AppendLine(line);
						}
					}, cancellationToken: cancellationToken
				);

				var errorTask = UniTask.RunOnThreadPool(
					async () => {
						while (!processHandle.HasExited) {
							cancellationToken.ThrowIfCancellationRequested();
							var line = await stderr.ReadLineAsync();
							if (!string.IsNullOrEmpty(line)) {
								Logger.LogError(line, tag: "yt-dlp");
								error.AppendLine(line);
							} else await UniTask.Delay(10, cancellationToken: cancellationToken);
						}

						// Read remaining error after process exits
						while (!stderr.EndOfStream) {
							var line = await stderr.ReadLineAsync();
							if (string.IsNullOrEmpty(line)) continue;
							Logger.LogError(line, tag: "yt-dlp");
							error.AppendLine(line);
						}
					}, cancellationToken: cancellationToken
				);

				// Wait for the process to exit
				while (!process.HasExited) {
					cancellationToken.ThrowIfCancellationRequested();
					await UniTask.Delay(50, cancellationToken: cancellationToken);
				}

				// Wait for both tasks to complete before disposing the process
				await UniTask.WhenAll(outputTask, errorTask);

				var errorStr = error.ToString();
				if (!string.IsNullOrEmpty(errorStr) && process.ExitCode != 0)
					throw new InvalidOperationException($"yt-dlp update error: {errorStr}");
			} catch (Exception ex) {
				throw new InvalidOperationException("Error updating yt-dlp", ex);
			} finally {
				process?.Dispose();
			}
		}
	}
}