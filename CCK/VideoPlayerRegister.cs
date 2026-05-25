using Nox.CCK.Events;
using Nox.VideoPlayer;

namespace Nox.CCK.VideoPlayer {
	public static class VideoPlayerRegister {
		public static readonly NoxEvent<IVideoPlayer> OnRegister = new();
		public static readonly NoxEvent<IVideoPlayer> OnUnRegister = new();

		public static void Register(IVideoPlayer player) {
			if (player == null)
				return;
			OnRegister.Invoke(player);
		}

		public static void UnRegister(IVideoPlayer player) {
			if (player == null)
				return;
			OnUnRegister.Invoke(player);
		}
	}
}