using System.Collections.Generic;
using System.Threading;
using Nox.VideoPlayer;

namespace Nox.CCK.VideoPlayer {
	public class VideoFetchOptions : IFetchOptions {
		public string                     Query        = string.Empty;
		public uint                       Page         = 0;
		public uint                       Limit        = 1;
		public Dictionary<string, object> Filters      = new();
		public CancellationTokenSource    Cancellation = new();

		public string GetQuery()
			=> Query;

		public uint GetPage()
			=> Page;

		public uint GetLimit()
			=> Limit;

		public Dictionary<string, object> GetFilters()
			=> Filters;

		public CancellationTokenSource GetCancellation()
			=> Cancellation;
	}
}