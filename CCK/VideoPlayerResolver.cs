using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nox.CCK.Events;
using Nox.VideoPlayer;

namespace Nox.CCK.VideoPlayer {
	public static class VideoPlayerResolver {
		public static readonly NoxEventAsync<IVideoPlayer, IFetchOptions, Action<IFetchOptions, IResult[]>> OnResolve = new();
		public static readonly NoxEvent<IVideoPlayer, string, Action<string, bool>> OnIsMedia = new();

		public static async UniTask<IResult[]> Resolve(IVideoPlayer player, IFetchOptions options) {
			var list = new List<IResult>();
			await OnResolve.InvokeAsync(player, options, Callback);
			return list.ToArray();
			void Callback(IFetchOptions initial, IResult[] results) {
				list.AddRange(results);
			}
		}

		public static bool IsMedia(IVideoPlayer player, string url) {
			var isMedia = false;
			OnIsMedia.Invoke(player, url, Callback);
			return isMedia;
			void Callback(string initial, bool result) {
				isMedia |= result;
			}
		}
	}
}