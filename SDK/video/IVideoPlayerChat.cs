using System;
using UnityEngine.Events;

namespace Nox.VideoPlayer {
	public interface IVideoPlayerChat {

		public ChatFlags Flags { get; }

		/// <summary>
		/// Event invoked when a chat message is received.
		/// </summary>
		public UnityEvent<IVideoPlayer, IChatMessage> OnMessage { get; }

		/// <summary>
		/// Sends a chat message.
		/// </summary>
		/// <param name="message">The message to send.</param>
		public void Send(IChatMessage message);
	}

	public interface IChatMessage {
		public string Author { get; }
		public string Content { get; }
	}

	[Flags]
	public enum ChatFlags {
		Read,
		Write
	}
}