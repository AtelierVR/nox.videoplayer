using System.Collections.Generic;
using UnityEngine;

namespace Nox.VideoPlayer.Runtime.Base {
	public class Format : IFormat {
		// Common
		public string Url       { get; set; }
		public Dictionary<string, string> Headers { get; set; }

		public string Container { get; set; }
		public string Language  { get; set; }
		public uint   Bitrate   { get; set; }
		public float  Quality   { get; set; }

		// Audio
		public uint   AudioChannels { get; set; }
		public uint   AudioBitrate  { get; set; }
		public string AudioCodec    { get; set; }

		// Video
		public Vector2Int Resolution   { get; set; }
		public uint       Framerate    { get; set; }
		public uint       VideoBitrate { get; set; }
		public string     VideoCodec   { get; set; }
		public string     DynamicRange { get; set; }

		public int CompareTo(IFormat other) {
			if (other == null) return 1;
			
			// Comparer d'abord par qualité
			var qualityComparison = Quality.CompareTo(other.Quality);
			if (qualityComparison != 0) return qualityComparison;

			// Puis par bitrate si les qualités sont égales
			var bitrateComparaison = Bitrate.CompareTo(other.Bitrate);
			if (bitrateComparaison != 0) return bitrateComparaison;

			var headersComparaison = Headers.GetHashCode() - other.Headers.GetHashCode();
			if (headersComparaison != 0) return headersComparaison;

			return 0;
		}
	}
}