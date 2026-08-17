using System;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Nox.Terminal;
using Nox.VideoPlayer;
using Nox.VideoPlayer.Runtime.Helpers;

namespace Nox.VideoPlayer.Runtime.Commands {
	/// <summary>
	/// Terminal command to manage registered video players.
	/// vp list / vp get &lt;id&gt; / vp play|pause|resume|stop &lt;id&gt; / vp url &lt;id&gt; [&lt;url&gt;]
	/// </summary>
	public class VideoPlayer : ICommand, IHelper {
		private const string Name = "vp";

		private static readonly string[] SubCommands = {
			"list", "get", "play", "pause", "resume", "stop", "url"
		};

		public string GetName()
			=> Name;

		public string GetDescription()
			=> "Manage video players.";

		public string GetShort()
			=> "Manage video players";

		public string GetUsage()
			=> "vp [list|get <id>|play <id>|pause <id>|resume <id>|stop <id>|url <id> [<url>]]";

		public string[] AutoComplete(string input, IContext context = null) {
			var lower = (input ?? string.Empty).ToLower();
			if (Name.StartsWith(lower))
				return new[] { Name };

			if (!lower.StartsWith(Name + " "))
				return Array.Empty<string>();

			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

			// Subcommand
			if (parts.Length == 2) {
				var sub = parts[1].ToLower();
				return SubCommands
					.Where(s => s.StartsWith(sub))
					.Select(s => $"{Name} {s}")
					.ToArray();
			}

			// Id
			if (parts.Length == 3 && SubCommands.Contains(parts[1].ToLower())) {
				var partial = parts[2].ToLower();
				return VideoPlayerManager.VideoPlayers
					.Select(p => p.GetId().ToString())
					.Where(id => id.StartsWith(partial))
					.Select(id => $"{Name} {parts[1].ToLower()} {id}")
					.ToArray();
			}

			return Array.Empty<string>();
		}

		public UniTask<bool> Execute(string input, IContext context = null)
			=> UniTask.FromResult(ExecuteInternal(input, context));

		private bool ExecuteInternal(string input, IContext context = null) {
			if (string.IsNullOrWhiteSpace(input) || context == null)
				return false;

			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (!parts[0].Equals(Name, StringComparison.OrdinalIgnoreCase))
				return false;

			var sub = parts.Length >= 2 ? parts[1].ToLower() : "list";

			switch (sub) {
				case "list":
					ListPlayers(context);
					return true;

				case "get":
					GetPlayer(parts, context);
					return true;

				case "play":
				case "pause":
				case "resume":
				case "stop":
					SetStatus(sub, parts, context);
					return true;

				case "url":
					SetUrl(parts, context);
					return true;

				default:
					context.PrintLn($"Unknown subcommand: {parts[1]}");
					context.PrintLn($"Usage: {GetUsage()}");
					return true;
			}
		}

		private void ListPlayers(IContext context) {
			var players = VideoPlayerManager.VideoPlayers;
			if (players.Count == 0) {
				context.PrintLn("No video players registered.");
				return;
			}

			context.PrintLn($"Video players ({players.Count}):");
			foreach (var player in players)
				context.PrintLn($"  {player.GetId()} - {GetTitle(player)}");
		}

		private void GetPlayer(string[] parts, IContext context) {
			var player = FindPlayer(parts, context);
			if (player == null) return;

			context.PrintLn($"ID:         {player.GetId()}");
			context.PrintLn($"GameObject: {player.GetGameObject()?.name ?? "null"}");
			context.PrintLn($"Title:      {GetTitle(player)}");
			if (player is IVideoPlayerDetails details)
				context.PrintLn($"Subtitle:   {details.Subtitle}");
			context.PrintLn($"URL:        {GetUrl(player) ?? "<none>"}");
			context.PrintLn($"Playing:    {player.IsPlaying}");
			context.PrintLn($"Time:       {Format(player.Time)} / {Format(player.Duration)} ({FormatPercent(player.Progress)})");
			context.PrintLn($"Volume:     {player.Volume:0.00}");
			context.PrintLn($"Loop:       {player.Loop}");
		}

		private void SetStatus(string status, string[] parts, IContext context) {
			var player = FindPlayer(parts, context);
			if (player == null) return;

			switch (status) {
				case "play":
					if (player.IsPlaying) {
						context.PrintLn("Already playing.");
						return;
					}

					var url = GetUrl(player);
					if (!string.IsNullOrWhiteSpace(url))
						player.Play(url);
					else
						player.Resume();
					context.PrintLn("Play started.");
					break;

				case "pause":
					player.Pause();
					context.PrintLn("Paused.");
					break;

				case "resume":
					player.Resume();
					context.PrintLn("Resumed.");
					break;

				case "stop":
					player.Stop();
					context.PrintLn("Stopped.");
					break;
			}
		}

		private void SetUrl(string[] parts, IContext context) {
			var player = FindPlayer(parts, context);
			if (player == null) return;

			// vp url <id>        → get
			// vp url <id> <url>  → set + play
			if (parts.Length >= 4) {
				var url = parts[3];
				player.Play(url);
				context.PrintLn($"Playing: {url}");
			} else {
				context.PrintLn($"URL: {GetUrl(player) ?? "<none>"}");
			}
		}

		private IVideoPlayer FindPlayer(string[] parts, IContext context) {
			if (parts.Length < 3 || !int.TryParse(parts[2], out var id)) {
				context.PrintLn($"Usage: {Name} {parts[1]} <id>");
				return null;
			}

			var player = VideoPlayerManager.VideoPlayers.FirstOrDefault(p => p.GetId() == id);
			if (player == null)
				context.PrintLn($"Video player {id} not found.");

			return player;
		}

		private static string GetTitle(IVideoPlayer player) {
			if (player is IVideoPlayerDetails details && !string.IsNullOrWhiteSpace(details.Title))
				return details.Title;
			return player.GetGameObject()?.name ?? "Unknown";
		}

		private static string GetUrl(IVideoPlayer player) {
			var type = player.GetType();

			var field = type.GetField("Url", BindingFlags.Public | BindingFlags.Instance);
			if (field != null && field.FieldType == typeof(string))
				return field.GetValue(player) as string;

			var property = type.GetProperty("Url", BindingFlags.Public | BindingFlags.Instance);
			if (property != null && property.PropertyType == typeof(string))
				return property.GetValue(player) as string;

			return null;
		}

		private static string Format(double value)
			=> double.IsNaN(value) || double.IsInfinity(value) ? "n/a" : value.ToString("0.0");

		private static string FormatPercent(double value)
			=> double.IsNaN(value) || double.IsInfinity(value) ? "n/a" : $"{(value * 100.0):0.0}%";
	}
}
