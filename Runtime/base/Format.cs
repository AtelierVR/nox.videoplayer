using Nox.VideoPlayer;
using UnityEngine;

namespace api.nox.videoplayer {
	public class Format : IFormat {
		// Common
		public string Url;
		public string Container;
		public string Language;
		public uint   Bitrate;
		public float  Quality;

		// Audio
		public uint   AudioChannels;
		public uint   AudioBitrate;
		public string AudioCodec;

		// Video
		public Vector2Int Resolution;
		public uint       Framerate;
		public uint       VideoBitrate;
		public string     VideoCodec;
		public string     DynamicRange;

		public string GetUrl()
			=> Url;

		public string GetContainer()
			=> Container;

		public string GetLanguage()
			=> Language;

		public uint GetBitrate()
			=> Bitrate;

		public float GetQuality()
			=> Quality;

		public uint GetAudioChannels()
			=> AudioChannels;

		public uint GetAudioBitrate()
			=> AudioBitrate;

		public string GetAudioCodec()
			=> AudioCodec;

		public Vector2Int GetResolution()
			=> Resolution;

		public uint GetFramerate()
			=> Framerate;

		public uint GetVideoBitrate()
			=> VideoBitrate;

		public string GetVideoCodec()
			=> VideoCodec;

		public string GetDynamicRange()
			=> DynamicRange;

		public int CompareTo(IFormat other) {
			if (other == null) return 1;
			
			// Comparer d'abord par qualité
			var qualityComparison = Quality.CompareTo(other.GetQuality());
			if (qualityComparison != 0) return qualityComparison;
			
			// Puis par bitrate si les qualités sont égales
			return Bitrate.CompareTo(other.GetBitrate());
		}
	}
}