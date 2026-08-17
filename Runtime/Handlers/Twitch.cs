using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.VideoPlayer;
using Newtonsoft.Json.Linq;
using Nox.VideoPlayer.Runtime.Base;
using Nox.VideoPlayer.Runtime.Processors;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Handlers {
	public class Twitch : IHandler {
		public const string SearchPrefix = "twitch:";
		public string Id
			=> "twitch";

		public string TitleKey
			=> "videoplayer.handler.twitch";

		public string[] TitleArguments
			=> new string[] { };

		public int EstimatePriority(IFetchOptions options)
			=> IsUrl(options.Query)
				|| options.Query.StartsWith(SearchPrefix)
					? 100
					: -1;

		private static bool IsUrl(string query)
			=> query.StartsWith("https://www.twitch.tv/");

		private static string FormatUrl(string original) {
			var id = "";

			if (original.StartsWith("https://www.twitch.tv/")) {
				var uri = new Uri(original);
				id = uri.AbsolutePath.TrimStart('/');
			}

			return $"https://www.twitch.tv/{id}";
		}

		public async UniTask<IResult[]> Fetch(IFetchOptions options) {
			try {
				if (EstimatePriority(options) < 0)
					return new IResult[] { Result.FromError("Query is not a valid Twitch URL") };

				var twitchUrl = options.Query.StartsWith(SearchPrefix)
					? $"https://www.twitch.tv/{options.Query[SearchPrefix.Length..]}"
					: FormatUrl(options.Query);
				var response = await YtDl.Extract(twitchUrl, cancellationToken: options.Cancellation.Token);

				if (response is not { Type: JTokenType.Object })
					throw new InvalidDataException("Response from yt-dlp is not an object");

				var type      = Global.ToObject(response["_type"], "unknown");
				var extractor = Global.ToObject(response["extractor"], "unknown");
				if (extractor != "twitch:stream" && extractor != "twitch")
					throw new InvalidDataException($"Unexpected extractor: {extractor}");

				return new IResult[] {
					Result.FromData(
						type switch {
							"video" => new[] { ParseVideo(response) },
							_       => throw new InvalidDataException($"Unknown response type: {type}")
						}
					)
				};
			} catch (Exception e) {
				Logger.LogError(e);
				return new IResult[] { Result.FromError(e.Message) };
			}
		}

		private static Resolve ParseVideo(JToken video) {
			if (video is not { Type: JTokenType.Object })
				throw new InvalidDataException("Video is not an object");

			return new Resolve {
				Id = video["id"]?.ToString() ?? "",
				Title = video["description"]?.ToString()
					?? video["title"]?.ToString()
					?? video["fulltitle"]?.ToString()
					?? video["id"]?.ToString(),
				Thumbnails = ParseThumbnails(video["thumbnails"]),
				Subtitles  = Array.Empty<Subtitle>(),
				Format     = ParseFormats(video["formats"])
			};
		}

		private static Thumbnail[] ParseThumbnails(JToken thumbnails) {
			if (thumbnails is not { Type: JTokenType.Array })
				return Array.Empty<Thumbnail>();

			return (from thumb in thumbnails
				where thumb is { Type: JTokenType.Object }
				let url = Global.ToObject(thumb["url"], "")
				where !string.IsNullOrWhiteSpace(url)
				select new Thumbnail { Url = url, Language = null, Resolution = ExtractResolutionFromThumbnailUrl(url) }).ToArray();
		}

		private static Vector2Int ExtractResolutionFromThumbnailUrl(string url) {
			try {
				var lastPart = url.Split('/').LastOrDefault();
				if (string.IsNullOrWhiteSpace(lastPart))
					return new Vector2Int(-1, -1);

				var sizePart = lastPart.Split('-').LastOrDefault();
				if (string.IsNullOrWhiteSpace(sizePart))
					return new Vector2Int(-1, -1);

				var dimensions = sizePart.Split('x');
				if (dimensions.Length != 2)
					return new Vector2Int(-1, -1);

				if (int.TryParse(dimensions[0], out var width) && int.TryParse(dimensions[1], out var height))
					return new Vector2Int(width, height);
			} catch (Exception e) {
				Logger.LogError(e);
			}

			return new Vector2Int(-1, -1);
		}

		private static Format[] ParseFormats(JToken formats) {
			if (formats is not { Type: JTokenType.Array })
				return System.Array.Empty<Format>();
			var list = new List<Format>();
			foreach (var format in formats) {
				if (format is not { Type: JTokenType.Object })
					continue;

				var acodec = Global.ToObject(format["acodec"], "none");
				var vcodec = Global.ToObject(format["vcodec"], "none");

				Format fmt;
				if (acodec != "none" && vcodec != "none")
					fmt = new AudioVideoFormat();
				else if (acodec != "none")
					fmt = new AudioFormat();
				else if (vcodec != "none")
					fmt = new VideoFormat();
				else
					continue;

				fmt.Url       = Global.ToObject(format["url"], "");
				fmt.Container = Global.ToObject(format["container"], "");
				fmt.Language  = Global.ToObject(format["language"], "");
				fmt.Bitrate   = Global.ToObject(format["tbr"], 0u);
				fmt.Quality   = Global.ToObject(format["quality"], 0f);

				if (fmt is AudioVideoFormat or VideoFormat) {
					fmt.Resolution = new Vector2Int(
						Global.ToObject(format["width"], 0),
						Global.ToObject(format["height"], 0)
					);
					fmt.Framerate    = Global.ToObject(format["fps"], 0u);
					fmt.VideoBitrate = Global.ToObject(format["vbr"], 0u);
					fmt.VideoCodec   = vcodec;
					fmt.DynamicRange = Global.ToObject(format["dynamic_range"], "SDR");
				}

				if (fmt is AudioVideoFormat or AudioFormat) {
					fmt.AudioBitrate = Global.ToObject(format["abr"], 0u);
					fmt.AudioCodec   = acodec;
				}

				if (!string.IsNullOrWhiteSpace(fmt.Url))
					list.Add(fmt);
			}

			return list.ToArray();
		}

		private static Resolve[] ParsePlaylist(JToken playlist) {
			if (playlist is not { Type: JTokenType.Object })
				throw new InvalidDataException("Playlist is not an object");
			var entries = playlist["entries"];
			return entries is not { Type: JTokenType.Array }
				? System.Array.Empty<Resolve>()
				: entries.Select(ParseVideo)
					.ToArray();
		}
	}
}