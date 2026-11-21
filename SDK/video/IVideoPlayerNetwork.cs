using Nox.Sessions;

namespace Nox.VideoPlayer {
	/// <summary>
	/// Marker for a video player that is a session entity: it is an <see cref="IVideoPlayer"/> (play,
	/// pause, stop, time, state, ...) replicated by the session it belongs to, so its URL, position,
	/// play state and lock flag are shared with the other clients.
	/// <para>
	/// The sync itself (URL, position, state, lock) is the business of the player implementation:
	/// this interface only tells that the player is an entity of the session.
	/// </para>
	/// </summary>
	public interface IVideoPlayerNetwork : ISessionEntity, IVideoPlayer {
	}
}
