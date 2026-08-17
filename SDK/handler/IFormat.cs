using System;

namespace Nox.VideoPlayer {
	public interface IFormat {
		public string Url { get; }

		public string Container { get; }

		public string Language { get; }

		public uint Bitrate { get; }

		public float Quality { get; }
	}
}