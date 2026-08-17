using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nox.VideoPlayer;
using Nox.VideoPlayer.Runtime.Base;
using Nox.VideoPlayer.Runtime.Processors;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Handlers {
	public class Global : IHandler {
		public string Id
			=> "global";

		public string TitleKey
			=> "videoplayer.handler.global";

		public string[] TitleArguments
			=> new string[] { };

		public static T ToObject<T>(JToken input, T @default) {
			switch (input) {
				case null:
				case { Type: JTokenType.Null }:
					return @default;
				default:
					try {
						return input.ToObject<T>();
					} catch {
						return @default;
					}
			}
		}

		public int EstimatePriority(IFetchOptions options) {
			if (IsUrl(options.Query))
				return 1;
			if (options.Query.StartsWith("search:"))
				return 1;
			return -1;
		}

		private static bool IsUrl(string query)
			=> query.StartsWith("https://") || query.StartsWith("http://")
				|| query.StartsWith("rtmp://") || query.StartsWith("rtmps://")
				|| query.StartsWith("rtsp://") || query.StartsWith("rtsps://")
				|| query.StartsWith("srt://") || query.StartsWith("hls://")
				|| query.StartsWith("mms://") || query.StartsWith("rtp://")
				|| query.StartsWith("udp://");

		private static string FormatUrl(string original)
			=> original;

		public async UniTask<IResult[]> Fetch(IFetchOptions options) {
			try {
				if (EstimatePriority(options) < 0)
					return new IResult[] { Result.FromError("Cannot handle this query") };
				var query = options.Query.StartsWith("search:")
					? options.Query.Substring("search:".Length)
					: options.Query;
				var response = await YtDl.Extract(query, cancellationToken: options.Cancellation.Token);
				if (response is not { Type: JTokenType.Object })
					throw new InvalidDataException("Response from yt-dlp is not an object");
				var type = ToObject(response["_type"], "unknown");
				return new IResult[] {
					Result.FromData(
						type switch {
							"video"    => new[] { ParseVideo(response) },
							"playlist" => ParsePlaylist(response),
							_          => throw new InvalidDataException($"Unknown response type: {type}")
						}
					)
				};
			} catch (System.Exception e) {
				Logger.LogError(e);
				return new IResult[] { Result.FromError(e.Message) };
			}
		}

		private static Resolve ParseVideo(JToken video) {
			if (video is not { Type: JTokenType.Object })
				throw new InvalidDataException("Video is not an object");

			return new Resolve {
				Id         = ToObject(video["id"], ""),
				Title      = ToObject(video["title"], ToObject(video["fulltitle"], ToObject(video["id"], ""))),
				Thumbnails = ParseThumbnails(video["thumbnails"]),
				Subtitles  = ParseSubtitles(video["subtitles"]),
				Format     = ParseFormats(video["formats"])
			};
		}

		private static Thumbnail[] ParseThumbnails(JToken thumbnails) {
			if (thumbnails is not { Type: JTokenType.Array })
				return System.Array.Empty<Thumbnail>();

			return (from thumb in thumbnails
				where thumb is { Type: JTokenType.Object }
				let url = ToObject(thumb["url"], "")
				where !string.IsNullOrWhiteSpace(url)
				let width = ToObject(thumb["width"], -1)
				let height = ToObject(thumb["height"], -1)
				select new Thumbnail { Url = url, Language = null, Resolution = new Vector2Int(width, height) }).ToArray();
		}

		private static Subtitle[] ParseSubtitles(JToken subtitles) {
			if (subtitles is not { Type: JTokenType.Object })
				return System.Array.Empty<Subtitle>();
			return (from entry in subtitles.Children<JProperty>()
				let lang = entry.Name
				where entry.Value is { Type: JTokenType.Array }
				from sub in entry.Value
				let ext = ToObject(sub["ext"], "")
				let url = ToObject(sub["url"], "")
				let name = ToObject(sub["name"], "")
				where !string.IsNullOrWhiteSpace(url) && ext == "srt"
				select new Subtitle { Url = url, Language = lang, Title = name }).ToArray();
		}

		private static Format[] ParseFormats(JToken formats) {
			if (formats is not { Type: JTokenType.Array })
				return System.Array.Empty<Format>();
			var list = new List<Format>();
			foreach (var format in formats) {
				if (format is not { Type: JTokenType.Object })
					continue;

				var acodec = ToObject(format["acodec"], "none");
				var vcodec = ToObject(format["vcodec"], "none");

				Format fmt;
				if (acodec != "none" && vcodec != "none")
					fmt = new AudioVideoFormat();
				else if (acodec != "none")
					fmt = new AudioFormat();
				else if (vcodec != "none")
					fmt = new VideoFormat();
				else
					continue;

				fmt.Url       = ToObject(format["url"], "");
				fmt.Container = ToObject(format["container"], "");
				fmt.Language  = ToObject(format["language"], "");
				fmt.Bitrate   = ToObject(format["bitrate"], 0u);
				fmt.Quality   = ToObject(format["quality"], 0f);

				if (fmt is AudioVideoFormat or VideoFormat) {
					fmt.Resolution = new Vector2Int(
						ToObject(format["width"], 0),
						ToObject(format["height"], 0)
					);
					fmt.Framerate    = ToObject(format["fps"], 0u);
					fmt.VideoBitrate = ToObject(format["tbr"], 0u);
					fmt.VideoCodec   = vcodec;
					fmt.DynamicRange = ToObject(format["dynamic_range"], "SDR");
				}

				if (fmt is AudioVideoFormat or AudioFormat) {
					fmt.AudioBitrate = ToObject(format["abr"], 0u);
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