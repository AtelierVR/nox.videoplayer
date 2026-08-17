using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nox.VideoPlayer {
	public interface IHandler {
		public string Id { get; }

		public string TitleKey { get; }

		public string[] TitleArguments { get; }

		public int EstimatePriority(IFetchOptions options);

		public UniTask<IResult[]> Fetch(IFetchOptions options);
	}
}