using System.Collections.Generic;
using System.Threading;

namespace Nox.VideoPlayer {
	public interface IFetchOptions {
		public string GetQuery();

		public uint GetPage();
		public uint GetLimit();

		public Dictionary<string, object> GetFilters();

		public CancellationTokenSource GetCancellation();
	}
}