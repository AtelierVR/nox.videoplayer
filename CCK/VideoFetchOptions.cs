using System.Collections.Generic;
using System.Threading;
using Nox.VideoPlayer;

namespace Nox.CCK.VideoPlayer {
	public class VideoFetchOptions : IFetchOptions {
		public string                     Query    { get; set; }     = string.Empty;
		public uint                       Page          { get; set; }     = 0;
		public uint                       Limit         { get; set; }     = 1;
		public Dictionary<string, object> Filters      { get; set; } = new();
		public CancellationTokenSource    Cancellation { get; set; } = new();
	}
}