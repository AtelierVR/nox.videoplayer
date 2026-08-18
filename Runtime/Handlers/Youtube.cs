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
using System.Text.RegularExpressions;

namespace Nox.VideoPlayer.Runtime.Handlers {
	public class Youtube : IHandler {
		public const string SearchPrefix = "youtube:";

		public string Id
			=> "youtube";

		public string TitleKey
			=> "videoplayer.handler.youtube";

		public string[] TitleArguments
			=> new string[] { };

		public int EstimatePriority(IFetchOptions options) {
			if (IsUrl(options.Query))
				return 100;
			if (options.Query.StartsWith(SearchPrefix))
				return 10;
			return -1;
		}

		private static readonly Regex YoutubeUrlRegex = new(
    		@"^https://(www\.|music\.)?youtube\.com/watch|^https://youtu\.be/",
    		RegexOptions.Compiled | RegexOptions.IgnoreCase
		);

		private static bool IsUrl(string query)
			=> YoutubeUrlRegex.IsMatch(query);

		private static string FormatUrl(string original) 
			=> original;
		/*{
			var id = "";

			if (original.StartsWith("https://www.youtube.com/watch")
				|| original.StartsWith("https://music.youtube.com/watch")) {
				var uri   = new System.Uri(original);
				var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
				id = query.Get("v") ?? "";
			} else if (original.StartsWith("https://youtu.be/")) {
				var uri = new System.Uri(original);
				id = uri.AbsolutePath.TrimStart('/');
			}

			return $"https://www.youtube.com/watch?v={id}";
		}*/

		public async UniTask<IResult[]> Fetch(IFetchOptions options) {
			try {
				if (EstimatePriority(options) < 0)
					return new IResult[] { Result.FromError("Cannot handle this query") };

				var searchQuery = options.Query.StartsWith(SearchPrefix)
					? options.Query.Substring(SearchPrefix.Length)
					: options.Query;
				var response = IsUrl(options.Query)
					? await YtDl.Extract(FormatUrl(options.Query), cancellationToken: options.Cancellation.Token)
					: await YtDl.Extract($"ytsearch{options.Limit}:{searchQuery}", cancellationToken: options.Cancellation.Token);

				if (response is not { Type: JTokenType.Object })
					throw new InvalidDataException("Response from yt-dlp is not an object");

				var type      = Global.ToObject(response["_type"], "unknown");
				var extractor = Global.ToObject(response["extractor"], "unknown");
				if (extractor != "youtube:search" && extractor != "youtube")
					throw new InvalidDataException($"Unexpected extractor: {extractor}");

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
				Id         = video["id"]?.ToString() ?? "",
				Title      = video["title"]?.ToString() ?? video["fulltitle"]?.ToString() ?? video["id"]?.ToString(),
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
				let url = Global.ToObject(thumb["url"], "")
				where !string.IsNullOrWhiteSpace(url)
				let width = Global.ToObject(thumb["width"], -1)
				let height = Global.ToObject(thumb["height"], -1)
				select new Thumbnail { Url = url, Language = null, Resolution = new Vector2Int(width, height) }).ToArray();
		}

		private static Subtitle[] ParseSubtitles(JToken subtitles) {
			if (subtitles is not { Type: JTokenType.Object })
				return System.Array.Empty<Subtitle>();
			return (from entry in subtitles.Children<JProperty>()
				let lang = entry.Name
				where entry.Value is { Type: JTokenType.Array }
				from sub in entry.Value
				let ext = Global.ToObject(sub["ext"], "")
				let url = Global.ToObject(sub["url"], "")
				let name = Global.ToObject(sub["name"], "")
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
				fmt.Bitrate   = Global.ToObject(format["bitrate"], 0u);
				fmt.Quality   = Global.ToObject(format["quality"], 0f);
				fmt.Headers   = Global.ToObject(format["http_headers"], new Dictionary<string, string>());

				if (fmt is AudioVideoFormat or VideoFormat) {
					fmt.Resolution = new Vector2Int(
						Global.ToObject(format["width"], 0),
						Global.ToObject(format["height"], 0)
					);
					fmt.Framerate    = Global.ToObject(format["fps"], 0u);
					fmt.VideoBitrate = Global.ToObject(format["tbr"], 0u);
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