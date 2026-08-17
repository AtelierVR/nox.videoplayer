using System.Linq;
using Nox.VideoPlayer;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Base {
	public class Resolve : IResolve {
		public string       Id          { get; set; }
		public string       Title       { get; set; }
		public string       Subtitle    { get; set; }
		public string       Description { get; set; }
		public IThumbnail[] Thumbnails  { get; set; }
		public IFormat[]    Format      { get; set; }
		public ISubtitle[]  Subtitles   { get; set; }

		public (IFormat, IFormat) FindQuality(float quality = -1) {
			var isBest = Mathf.Approximately(quality, -1);
			if (isBest) {
				var best = Format
					.OrderBy(f => f.Quality)
					.ThenBy(f => f.Bitrate)
					.LastOrDefault();
				if (best != null) {
					quality = best.Quality;
					Logger.LogDebug($"No quality specified, using best quality {quality}");
				}
			}

			var merged = Format
				.OfType<AudioVideoFormat>()
				.Where(f => isBest || Mathf.Approximately(f.Quality, quality))
				.OrderBy(f => f.Bitrate)
				.LastOrDefault();

			var video = Format
				.OfType<VideoFormat>()
				.Where(f => isBest || Mathf.Approximately(f.Quality, quality))
				.OrderBy(f => f.Bitrate)
				.LastOrDefault();

			var audio = Format
				.OfType<AudioFormat>()
				.Where(f => isBest || f.Quality <= quality)
				.OrderBy(f => f.Quality)
				.ThenBy(f => f.Bitrate)
				.LastOrDefault();

			// YouTube serves separate DASH video/audio streams above ~720p; prefer them
			// over a lower-quality merged stream when they are higher quality.
			if (video != null && audio != null
				&& (merged == null || video.Quality > merged.Quality || video.Bitrate > merged.Bitrate))
				return (video, audio);

			if (merged != null)
				return (merged, null);

			if (video != null)
				return (video, audio);

			if (audio != null)
				return (audio, null);

			Logger.LogWarning($"No format found for quality {quality}");
			return (null, null);
		}
	}
}