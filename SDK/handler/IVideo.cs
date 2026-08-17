using UnityEngine;

namespace Nox.VideoPlayer {
	public interface IVideo : IFormat {
		public Vector2Int Resolution { get; }

		public uint Framerate { get; }

		public uint VideoBitrate { get; }

		public string VideoCodec { get; }

		public string DynamicRange { get; }
	}
}