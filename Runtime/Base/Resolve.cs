using System.Linq;
using Nox.VideoPlayer;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Base {
	public class Resolve : IResolve {
		public string      Id;
		public string      Title;
		public string      Subtitle;
		public string      Description;
		public Thumbnail[] Thumbnails;
		public Format[]    Formats;
		public Subtitle[]  Subtitles;

		public string GetId()
			=> Id;

		public string GetTile()
			=> Title;

		public string GetSubtitle()
			=> Subtitle;

		public string GetDescription()
			=> Description;

		public IThumbnail[] GetThumbnails()
			=> Thumbnails.Cast<IThumbnail>().ToArray();

		public IFormat[] GetFormat()
			=> Formats.Cast<IFormat>().ToArray();

		public ISubtitle[] GetSubtitles()
			=> Subtitles.Cast<ISubtitle>().ToArray();

		public (IFormat, IFormat) FindQuality(float quality = -1) {
			if (Mathf.Approximately(quality, -1)) {
				var best = Formats
					.OrderBy(f => f.Bitrate)
					.LastOrDefault();
				if (best != null) {
					quality = best.Quality;
					Logger.LogDebug($"No quality specified, using best quality {quality}");
				}
			}

			var format = Formats
				.OfType<AudioVideoFormat>()
				.OrderBy(f => f.Bitrate)
				.LastOrDefault(f => Mathf.Approximately(f.Quality, quality));

			if (format != null)
				return (format, null);

			var video = Formats
				.OfType<VideoFormat>()
				.OrderBy(f => f.Bitrate)
				.LastOrDefault(f => Mathf.Approximately(f.Quality, quality));

			var audio = Formats
				.OfType<AudioFormat>()
				.Where(f => f.Quality <= quality)
				.OrderBy(f => f.Quality)
				.LastOrDefault();

			if (video != null && audio != null)
				return (video, audio);

			Logger.LogWarning($"No format found for quality {quality}");
			return (null, null);
		}
	}
}