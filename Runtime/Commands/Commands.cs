using System;
using Nox.Terminal;
using Nox.VideoPlayer.Runtime;

namespace Nox.VideoPlayer.Runtime.Commands {
	/// <summary>
	/// Registers terminal commands for the video player mod.
	/// </summary>
	public class VideoPlayerCommands : IDisposable {
		internal static ITerminalAPI TerminalAPI
			=> Main.Instance.CoreAPI.ModAPI
				.GetMod("terminal")
				.GetInstance<ITerminalAPI>();

		private readonly (uint, ICommand)[] _list = {
			(0, new VideoPlayer())
		};

		public VideoPlayerCommands() {
			for (var i = 0; i < _list.Length; i++)
				_list[i] = (TerminalAPI.Register(_list[i].Item2), _list[i].Item2);
		}

		public void Dispose() {
			for (var i = 0; i < _list.Length; i++)
				TerminalAPI.Unregister(_list[i].Item1);
		}
	}
}
