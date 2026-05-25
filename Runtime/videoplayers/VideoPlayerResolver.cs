using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using Nox.VideoPlayer;
using CCKResolver = Nox.CCK.VideoPlayer.VideoPlayerResolver;

namespace api.nox.videoplayer {
	public static class VideoPlayerResolver {
		public static void Listen() {
			CCKResolver.OnResolve.AddListener(OnResolvingAsync);
			CCKResolver.OnIsMedia.AddListener(OnIsMedia);
		}


		public static void UnListen() {
			CCKResolver.OnResolve.RemoveListener(OnResolvingAsync);
			CCKResolver.OnIsMedia.RemoveListener(OnIsMedia);
		}
		public static readonly List<Regex> MediaRegexes = new() {
			// Direct media links — should be passed directly to the video player without going through a search engine
			new Regex(@"^https?://.*\.(mp4|webm|ogg|mp3|wav|flac|aac|m4a|opus|avi|mkv|mpeg|mpg|mov|flv|swf|3gp|3g2|ogg|opus|oga|spx|opus)(\?.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			// Streaming protocols — must never be passed to a search engine
			new Regex(@"^(rtmp|rtmps|rtsp|rtsps|srt|hls|mms|mmsh|mmst|rtp|udp)://", RegexOptions.IgnoreCase | RegexOptions.Compiled),
		};

		private static void OnIsMedia(IVideoPlayer player, string url, Action<string, bool> callback) {
			var isMedia = MediaRegexes.Any(r => r.IsMatch(url));
			callback(url, isMedia);
		}

		private static async UniTask OnResolvingAsync(IVideoPlayer player, IFetchOptions options, Action<IFetchOptions, IResult[]> callback) {
			await UniTask.SwitchToThreadPool();

			Logger.LogDebug($"Resolving video for player {player} with options {options}");

			var handlers = Main.Handlers
				.Select(e => (e.EstimatePriority(options), e))
				.Where(e => e.Item1 >= 0)
				.OrderByDescending(e => e.Item1)
				.Select(e => e.e)
				.ToArray();

			var results = new List<IResult>();

			await UniTask.SwitchToMainThread();
			await UniTask.WhenAll(Enumerable.Select(handlers, Resolver));

			callback(options, results.ToArray());

			return;

			async UniTask Resolver(IHandler handler) {
				var res = await handler.Fetch(options);
				if (res == null || res.Length == 0)
					return;
				results.AddRange(res);
			}
		}
	}
}