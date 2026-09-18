using System.Collections.Generic;
using UnityEngine.Events;

namespace Nox.VideoPlayer {
	public interface ITrack {
	    /// <summary>
	    /// The index (in <see cref="Names"/>) of the currently selected track.
	    /// Set it to switch track.
	    /// </summary>
        public int Selected { get; set; }
        
	    /// <summary>
	    /// The names of the available tracks.
	    /// </summary>
	    public IReadOnlyList<string> Names { get; }
        
	    /// <summary>
	    /// Event invoked when the selected track changes.
	    /// </summary>
	    public UnityEvent<IVideoPlayer, int> OnSelected { get; }

	    /// <summary>
	    /// Event invoked when the track list changes.
	    /// </summary>
	    public UnityEvent<IVideoPlayer, IReadOnlyList<string>> OnChanged { get; }
	}
}