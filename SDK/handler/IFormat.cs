using System.Collections.Generic;

namespace Nox.VideoPlayer {
	public interface IFormat {
		public string Url { get; }

		public Dictionary<string, string> Headers { get; }

		public string Container { get; }

		public string Language { get; }

		public uint Bitrate { get; }

		public float Quality { get; }
	}
}