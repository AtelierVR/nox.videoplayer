using System.Collections.Generic;
using System.Threading;

namespace Nox.VideoPlayer {
	public interface IFetchOptions {
		public string Query { get; set; }

		public uint Page { get; set; }
		public uint Limit { get; set; }

		public Dictionary<string, object> Filters { get; set; }

		public CancellationTokenSource Cancellation { get; set; }
	}
}