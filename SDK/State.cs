namespace Nox.VideoPlayer {
    public enum State {
        Playing,
        Paused,
        Stopped,
        Buffering,
        Stalled,
        Ended,
        /// <summary>
        /// The player is repositioning to a requested time (see <c>IVideoPlayer.Time</c>):
        /// the clocks are invalidated until playback resumes at the new position.
        /// </summary>
        Seeking
    }
}