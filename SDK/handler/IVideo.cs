using UnityEngine;

namespace Nox.VideoPlayer {
	public interface IVideo : IFormat {
		public Vector2Int GetResolution();

		public uint GetFramerate();

		public uint GetVideoBitrate();

		public string GetVideoCodec();

		public string GetDynamicRange();
	}
}